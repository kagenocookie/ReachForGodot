namespace ReaGE;

using Godot;
using RszTool;
using RszTool.Common;

public abstract class RszAssetConverter<TResource, TFile, TAsset> : RszToolConverter<TResource, TFile, TAsset>
    where TAsset : GodotObject
    where TFile : BaseFile
    where TResource : REResource
{
    protected readonly Dictionary<RszInstance, REObject> importedObjects = new();
    protected readonly Dictionary<REObject, RszInstance> objectSourceInstances = new();
    protected readonly Dictionary<REObject, RszInstance> exportedInstances = new();
    protected readonly Dictionary<Guid, GameObject> gameObjects = new();

    protected RszFileOption FileOption => Convert.FileOption;

    protected REObject CreateOrUpdateObject(RszInstance instance, REObject? obj)
    {
        return obj == null ? CreateOrGetObject(instance) : ApplyObjectValues(obj, instance);
    }

    public override void Clear()
    {
        importedObjects.Clear();
        objectSourceInstances.Clear();
        exportedInstances.Clear();
        gameObjects.Clear();
    }

    protected REObject CreateOrGetObject(RszInstance instance)
    {
        if (importedObjects.TryGetValue(instance, out var obj)) {
            return obj;
        }

        Debug.Assert(instance.RSZUserData == null, "Attempted to assign userdata to object field");

        obj = new REObject(Game, instance.RszClass.name);
        objectSourceInstances[obj] = instance;
        return ApplyObjectValues(obj, instance);
    }

    protected UserdataResource FindOrCreateEmbeddedUserdata(RSZFile rszFile)
    {
        var instance = rszFile.ObjectList[0];
        var assetPath = instance.Values[0] as string;
        string? importPath = null;
        if (!string.IsNullOrEmpty(assetPath)) {
            // example:  assets:/LevelDesign/Mission/Mission71/RedSeal/SM/M71_RedSealBundleAppear_SM.user.json
            if (!assetPath.StartsWith("assets:/") || !assetPath.EndsWith(".user.json")) {
                ErrorLog("Invalid embedded userdata path");
            } else {
                importPath = ProjectSettings.LocalizePath(Config.ImportBasePath + assetPath[("assets:/".Length..^(".json".Length))] + ".tres");
            }
        }

        if (importPath != null && (Convert.TryGetImportedResource(importPath, out var imported) || ResourceLoader.Exists(importPath) && Convert.Options.userdata < RszImportType.ForceReimport)) {
            var importedUser = imported as UserdataResource ?? ResourceLoader.Load<UserdataResource>(importPath);
            Convert.AddResource(importPath, imported);
            return importedUser;
        }

        var userdata = new UserdataResource() { Game = Game };
        var obj = new REObject(Game, instance.RszClass.name);
        objectSourceInstances[obj] = instance;
        ApplyObjectValues(obj, instance);
        userdata.Data = obj;

        if (importPath != null) {
            Convert.AddResource(importPath, userdata);
            if (WritesEnabled) {
                userdata.SaveOrReplaceResource(importPath);
            }
        }

        return userdata;
    }

    protected REObject ApplyObjectValues(REObject obj, RszInstance instance)
    {
        importedObjects[instance] = obj;
        objectSourceInstances[obj] = instance;
        foreach (var field in obj.TypeInfo.Fields) {
            var value = instance.Values[field.FieldIndex];
            obj.SetField(field, ConvertRszValue(field, value));
        }

        return obj;
    }


    private Variant ConvertRszValue(REField field, object value)
    {
        if (field.RszField.array) {
            if (value == null) return new Godot.Collections.Array();

            var type = value.GetType();
            object[] arr = ((IList<object>)value).ToArray();
            var newArray = new Godot.Collections.Array();
            foreach (var v in arr) {
                newArray.Add(ConvertSingleRszValue(field, v));
            }
            return newArray;
        }

        return ConvertSingleRszValue(field, value);
    }

    private Variant ConvertSingleRszValue(REField field, object value)
    {
        switch (field.RszField.type) {
            case RszFieldType.UserData:
                return ConvertUserdata((RszInstance)value);
            case RszFieldType.Object:
                var fieldInst = (RszInstance)value;
                return fieldInst.Index == 0 ? new Variant() : CreateOrGetObject(fieldInst);
            case RszFieldType.Resource:
                if (value is string str && !string.IsNullOrWhiteSpace(str)) {
                    if (Convert.TryGetImportedResource(str, out var res)) {
                        return res;
                    }
                    res = Importer.FindOrImportResource<REResource>(str, Config, WritesEnabled)!;
                    Convert.AddResource(str, res);
                    return res;
                } else {
                    return new Variant();
                }
            default:
                return RszTypeConverter.FromRszValueSingleValue(field.RszField.type, value, Game, field.RszField.original_type);
        }
    }

    private Variant ConvertRszInstanceArray(object value)
    {
        var values = (IList<object>)value;
        var newArray = new Godot.Collections.Array();
        foreach (var element in values) {
            var rsz = element as RszInstance;
            Debug.Assert(rsz != null);
            newArray.Add(rsz.Index == 0 ? new Variant() : CreateOrGetObject(rsz));
        }
        return newArray;
    }

    private Variant ConvertUserdata(RszInstance rsz)
    {
        if (importedObjects.TryGetValue(rsz, out var previousInst)) {
            return previousInst;
        }
        if (rsz.Index == 0) return new Variant();

        if (rsz.RSZUserData is RSZUserDataInfo ud1) {
            if (!string.IsNullOrEmpty(ud1.Path)) {
                if (Convert.TryGetImportedResource(ud1.Path, out var res)) {
                    return res;
                }
                var userdataResource = Importer.FindOrImportResource<UserdataResource>(ud1.Path, Config, WritesEnabled);
                if (userdataResource != null) {
                    if (userdataResource.IsEmpty == true) {
                        userdataResource.Data.ChangeClassname(rsz.RszClass.name);
                        if (WritesEnabled) ResourceSaver.Save(userdataResource);
                    }

                    Convert.AddResource(ud1.Path, userdataResource);
                    return userdataResource;
                }
                return new Variant();
            }
        } else if (rsz.RSZUserData is RSZUserDataInfo_TDB_LE_67 ud2) {
            if (ud2.EmbeddedRSZ != null) {
                return FindOrCreateEmbeddedUserdata(ud2.EmbeddedRSZ);
            } else {
                ErrorLog("Embedded userdata is null");
            }
        }
        Debug.Assert(string.IsNullOrEmpty(rsz.RszClass.name));
        return new Variant();
    }

    protected RszInstance ConstructUserdataInstance(UserdataResource userdata, RSZFile rsz, RszFileOption fileOption, BaseRszFile? container, bool isRoot = false)
    {
        if (exportedInstances.TryGetValue(userdata.Data, out var instance)) {
            return instance;
        }
        RszClass rszClass;
        rszClass = userdata.Data.TypeInfo.RszClass;
        if (string.IsNullOrEmpty(userdata.Classname)) {
            userdata.Reimport();
            if (string.IsNullOrEmpty(userdata.Classname)) {
                throw new ArgumentNullException("Missing root userdata classname " + userdata.Classname);
            }
        }
        IRSZUserDataInfo userdataInfo;
        if (Game.UsesEmbeddedUserdata()) {
            var path = userdata.Data.GetField(0).AsString();
            var pathHash = MurMur3HashUtils.GetHash(path);
            var info = rsz.RSZUserDataInfoList.OfType<RSZUserDataInfo_TDB_LE_67>().FirstOrDefault(u => u.jsonPathHash == pathHash);
            if (info == null) {
                info = EmbedUserdata(rsz, fileOption, userdata);
            }
            userdataInfo = info;
        } else {
            var path = userdata.Asset!.ExportedFilename;
            var info = rsz.RSZUserDataInfoList.OfType<RSZUserDataInfo>().FirstOrDefault(u => u.Path == path);
            if (info == null) {
                info = new RSZUserDataInfo() { typeId = rszClass.typeId, Path = path, instanceId = rsz.InstanceList.Count };
                rsz.RSZUserDataInfoList.Add(info);

                var fileUserdataList = (container as PfbFile)?.UserdataInfoList ?? (container as ScnFile)?.UserdataInfoList ?? (container as UserFile)?.UserdataInfoList;
                fileUserdataList!.Add(new UserdataInfo() { CRC = rszClass.crc, typeId = rszClass.typeId, Path = path });
            }
            userdataInfo = info;
        }

        // instance = new RszInstance(rszClass, rsz.InstanceInfoList.Count, userdataInfo, []);
        instance = new RszInstance(rszClass, userdataInfo.InstanceId, userdataInfo, []);
        instance.Index = rsz.InstanceList.Count;
        rsz.InstanceInfoList.Add(new InstanceInfo(fileOption.Version) { ClassName = rszClass.name, CRC = rszClass.crc, typeId = rszClass.typeId });
        rsz.InstanceList.Add(instance);
        exportedInstances[userdata.Data] = instance;
        return instance;
    }

    private RSZUserDataInfo_TDB_LE_67 EmbedUserdata(RSZFile parentRsz, RszFileOption fileOption, UserdataResource userdata)
    {
        var rszClass = userdata.Data.TypeInfo.RszClass;
        var embedrsz = new RSZFile(fileOption, parentRsz.FileHandler);
        embedrsz.ClearInstances();
        var embedInstance = ExportREObject(userdata.Data, embedrsz, fileOption, null);
        embedrsz.AddToObjectTable(embedInstance);
        var exportUserdata = new RSZUserDataInfo_TDB_LE_67() {
            instanceId = embedInstance.Index,
            EmbeddedRSZ = embedrsz,
            ClassName = userdata.Classname,
            jsonPathHash = MurMur3HashUtils.GetHash(userdata.Data.GetField(0).AsString()),
            typeId = rszClass.typeId,
        };
        parentRsz.RSZUserDataInfoList.Add(exportUserdata);
        parentRsz.EmbeddedRSZFileList ??= new();
        parentRsz.EmbeddedRSZFileList.Add(embedrsz);
        return exportUserdata;
    }

    protected void GenerateResources(IRszContainer root, List<ResourceInfo> resourceInfos)
    {
        var resources = new List<REResource>(resourceInfos.Count);
        foreach (var res in resourceInfos) {
            if (!string.IsNullOrWhiteSpace(res.Path)) {
                var resource = Importer.FindOrImportResource<REResource>(res.Path, Config, WritesEnabled)
                    ?? new REResource() {
                        Asset = new AssetReference(res.Path),
                        Game = Game,
                        ResourceName = res.Path.GetFile()
                    };
                resources.Add(resource);
            } else {
                Log("Found a resource with null path: " + resources.Count);
            }
        }
        root.Resources = resources.ToArray();
    }

    protected void StoreResources(REResource[]? resources, List<ResourceInfo> list, bool isPfb)
    {
        if (resources != null) {
            foreach (var res in resources) {
                list.Add(new ResourceInfo(FileOption.Version, isPfb) { Path = res.Asset?.ExportedFilename });
            }
        }
    }

    protected static void AddMissingResourceInfos(GameObject gameObject, IRszContainer source)
    {
        foreach (var comp in gameObject.Components) {
            foreach (var res in comp.NestedResources()) {
                if (res is not UserdataResource && source.EnsureContainsResource(res)) {
                    GD.Print("Added missing resource to resources list: " + res.Asset?.AssetFilename);
                }
            }
        }
    }

    protected RszInstance ExportREObject(REObject target, RSZFile rsz, RszFileOption fileOption, BaseRszFile? container)
    {
        if (exportedInstances.TryGetValue(target, out var instance)) {
            return instance;
        }
        int i = 0;
        RszClass rszClass;
        if (string.IsNullOrEmpty(target.Classname)) {
            throw new ArgumentNullException("Missing root REObject classname " + target.Classname);
        }
        rszClass = target.TypeInfo.RszClass;
        instance = new RszInstance(rszClass, rsz.InstanceList.Count, null, new object[target.TypeInfo.Fields.Length]);

        var values = instance.Values;
        foreach (var field in target.TypeInfo.Fields) {
            if (!target.TryGetFieldValue(field, out var value)) {
                values[i++] = field.RszField.array ? new List<object>(0) : RszInstance.CreateNormalObject(field.RszField);
                continue;
            }

            switch (field.RszField.type) {
                case RszFieldType.Object:
                    if (field.RszField.array) {
                        var array = value.AsGodotArray<REObject>() ?? throw new Exception("Unhandled rsz object array type");
                        var array_refs = new object[array.Count];
                        for (int arr_idx = 0; arr_idx < array.Count; ++arr_idx) {
                            var val = array[arr_idx];
                            array_refs[arr_idx] = val == null ? 0 : (object)ExportREObject(array[arr_idx], rsz, fileOption, container);
                        }
                        values[i++] = array_refs;
                    } else if (value.VariantType == Variant.Type.Nil) {
                        values[i++] = 0; // index 0 is the null instance entry
                    } else {
                        var obj = value.As<REObject>() ?? throw new Exception("Unhandled rsz object array type");
                        values[i++] = ExportREObject(obj, rsz, fileOption, container);
                    }
                    break;
                case RszFieldType.UserData:
                    if (field.RszField.array) {
                        var array = value.AsGodotArray<UserdataResource>() ?? throw new Exception("Unhandled rsz object array type");
                        var array_refs = new object[array.Count];
                        for (int arr_idx = 0; arr_idx < array.Count; ++arr_idx) {
                            var val = array[arr_idx];
                            array_refs[arr_idx] = val == null ? 0 : (object)ConstructUserdataInstance(array[arr_idx], rsz, fileOption, container);
                        }
                        values[i++] = array_refs;
                    } else if (value.VariantType == Variant.Type.Nil) {
                        values[i++] = 0; // index 0 is the null instance entry
                    } else {
                        var obj = value.As<UserdataResource>() ?? throw new Exception("Unhandled rsz object array type");
                        values[i++] = ConstructUserdataInstance(obj, rsz, fileOption, container);
                    }
                    break;
                case RszFieldType.Resource:
                    if (field.RszField.array) {
                        values[i++] = value.AsGodotArray<REResource>().Select(obj => obj?.Asset?.ExportedFilename ?? string.Empty).ToArray();
                    } else {
                        values[i++] = value.As<REResource?>()?.Asset?.ExportedFilename ?? string.Empty;
                    }
                    break;
                case RszFieldType.GameObjectRef:
                    if (field.RszField.array) {
                        values[i++] = value.AsGodotArray<GameObjectRef>().Select(p => (object)p.TargetGuid).ToArray();
                    } else {
                        values[i++] = value.As<GameObjectRef>().TargetGuid;
                    }
                    break;
                default:
                    var converted = RszTypeConverter.ToRszStruct(value, field, target.Game);
                    values[i++] = converted ?? RszInstance.CreateNormalObject(field.RszField);
                    break;
            }
        }

        instance.Index = rsz.InstanceList.Count;
        rsz.InstanceInfoList.Add(new InstanceInfo(fileOption.Version) { ClassName = rszClass.name, CRC = rszClass.crc, typeId = rszClass.typeId });
        rsz.InstanceList.Add(instance);
        exportedInstances[target] = instance;
        return instance;
    }

    protected void PrepareGameObjectBatch(IGameObject data, AssetConverter.GameObjectBatch batch, Node? parentNode, GameObject? parent = null, int dedupeIndex = 0)
    {
        var name = !string.IsNullOrEmpty(data.Name) ? data.Name : "UnnamedGameObject";

        Debug.Assert(data.Instance != null);

        GameObject? gameobj = batch.GameObject;
        if (gameobj == null && parentNode != null) {
            if (parentNode is GameObject obj) {
                gameobj = obj.GetChild(name, dedupeIndex);
            } else if (parentNode is SceneFolder scn) {
                gameobj = scn.GetGameObject(name, dedupeIndex);
            } else {
                Debug.Assert(false);
            }
            batch.GameObject = gameobj!;
        }

        string? prefabPath = null;

        if (data is RszTool.Scn.ScnGameObject scnData) {
            // note: some PFB files aren't shipped with the game, hence the CheckResourceExists check
            // presumably they are only used directly within scn files and not instantiated during runtime
            prefabPath = scnData.Prefab?.Path;
            if (!string.IsNullOrEmpty(prefabPath) && Importer.CheckResourceExists(prefabPath, Config, false)) {
                var importFilepath = PathUtils.GetLocalizedImportPath(prefabPath, Config)!;
                if (!Convert.HasImportedResource(importFilepath) && Importer.FindOrImportAsset<PackedScene>(prefabPath, Config, WritesEnabled) is PackedScene packedPfb) {
                    if (Convert.Options.prefabs == RszImportType.Placeholders) {
                        var pfbInstance = packedPfb.Instantiate<PrefabNode>(PackedScene.GenEditState.Instance);
                        pfbInstance.Name = name;
                        if (data.Components.FirstOrDefault(t => t.RszClass.name == "via.Transform") is RszInstance transform) {
                            RETransformComponent.ApplyTransform(pfbInstance, transform);
                        }
                        return;
                    }
                    return;
                }
            }
        }

        var isnew = false;
        if (gameobj == null) {
            isnew = true;
            gameobj = string.IsNullOrEmpty(prefabPath) ? new GameObject() {
                Game = Game,
                Name = name,
                OriginalName = name,
            } : new PrefabNode() {
                Game = Game,
                Name = name,
                Prefab = prefabPath,
                OriginalName = name,
            };
        } else {
            gameobj.OriginalName = name;
        }
        batch.GameObject = gameobj;

        if (data is RszTool.Scn.ScnGameObject scnData2) {
            var guid = scnData2.Info!.guid;
            gameobj.Uuid = guid.ToString();
            gameObjects[guid] = gameobj;
        }

        gameobj.Data = CreateOrUpdateObject(data.Instance, gameobj.Data);

        if (gameobj.GetParent() == null && parentNode != null && parentNode != gameobj) {
            parentNode.AddUniqueNamedChild(gameobj);
            var owner = gameobj.FindRszOwnerNode();
            Debug.Assert(owner != gameobj);
            gameobj.Owner = owner;
            if (gameobj is PrefabNode) {
                owner?.SetEditableInstance(gameobj, true);
            }
        }

        foreach (var comp in data.Components.OrderBy(o => o.Index)) {
            SetupComponent(comp, batch, Convert.Options.assets);
        }

        var dupeDict = new Dictionary<string, int>();
        foreach (var child in data.GetChildren().OrderBy(o => o.Instance!.Index)) {
            var childName = child.Name ?? "";
            if (dupeDict.TryGetValue(childName, out var index)) {
                dupeDict[childName] = ++index;
            } else {
                dupeDict[childName] = index = 0;
            }
            var childBatch = Convert.CreateGameObjectBatch(gameobj.Path + "/" + childName + index);
            batch.Children.Add(childBatch);
            PrepareGameObjectBatch(child, childBatch, gameobj, gameobj, index);
        }
        if (isnew && dupeDict.Count == 0) {
            gameobj.SetDisplayFolded(true);
        }
    }

    private void SetupComponent(RszInstance instance, AssetConverter.GameObjectBatch batch, RszImportType importType)
    {
        if (Game == SupportedGame.Unknown) {
            ErrorLog("Game required on rsz container root for SetupComponent");
            return;
        }
        var gameObject = batch.GameObject;

        var classname = instance.RszClass.name;
        var componentInfo = gameObject.GetComponent(classname);
        if (componentInfo != null) {
            // nothing to do here
        } else if (TypeCache.TryCreateComponent(Game, classname, gameObject, instance, out componentInfo)) {
            if (componentInfo == null) {
                componentInfo = new REComponentPlaceholder(Game, classname);
                gameObject.AddComponent(componentInfo);
            } else if (gameObject.GetComponent(classname) == null) {
                // if the component was created but not actually added to the gameobject yet, do so now
                gameObject.AddComponent(componentInfo);
            }
        } else {
            componentInfo = new REComponentPlaceholder(Game, classname);
            gameObject.AddComponent(componentInfo);
        }

        componentInfo.GameObject = gameObject;
        ApplyObjectValues(componentInfo, instance);
        var setupTask = componentInfo.Setup(Convert.Options.assets);
        if (!setupTask.IsCompleted) {
            batch.ComponentTasks.Add(setupTask);
        }
    }

    protected void SetupGameObjectReferenceGuids(GameObject gameobj, Node root)
    {
        foreach (var comp in gameobj.Components) {
            RecurseSetupGameObjectReferenceGuids(comp, comp, root);
        }

        foreach (var child in gameobj.Children) {
            SetupGameObjectReferenceGuids(child, root);
        }
    }

    protected void RecurseSetupGameObjectReferenceGuids(REObject data, REComponent component, Node root)
    {
        foreach (var field in data.TypeInfo.Fields) {
            if (field.RszField.IsGameObjectRef()) {
                if (!exportedInstances.TryGetValue(data, out var dataInst)) {
                    GD.PrintErr($"Could not resolve GameObjectRef source instance for field {field.SerializedName} in {component.Path}");
                    continue;
                }

                if (data.TryGetFieldValue(field, out var value)) {
                    if (field.RszField.array) {
                        var refs = value.AsGodotArray<GameObjectRef>();
                        var values = new object[refs.Count];
                        int i = 0;
                        foreach (var path in refs) {
                            values[i++] = path.ResolveGuid(component.GameObject);
                        }
                        dataInst.Values[field.FieldIndex] = values;
                    } else {
                        if (value.As<GameObjectRef>() is GameObjectRef goref && !goref.IsEmpty) {
                            dataInst.Values[field.FieldIndex] = goref.ResolveGuid(component.GameObject);
                        }
                    }
                }
            } else if (field.RszField.type == RszFieldType.Object) {
                if (data.TryGetFieldValue(field, out var child)) {
                    if (field.RszField.array) {
                        if (child.AsGodotArray<REObject>() is Godot.Collections.Array<REObject> children) {
                            foreach (var childObj in children) {
                                if (childObj != null) {
                                    RecurseSetupGameObjectReferenceGuids(childObj, component, root);
                                }
                            }
                        }
                    } else {
                        if (child.VariantType != Variant.Type.Nil && child.As<REObject>() is REObject childObj) {
                            RecurseSetupGameObjectReferenceGuids(childObj, component, root);
                        }
                    }
                }
            }
        }
    }

}
