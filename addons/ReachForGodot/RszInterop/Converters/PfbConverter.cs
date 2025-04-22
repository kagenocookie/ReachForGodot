namespace ReaGE;

using System;
using System.Threading.Tasks;
using Godot;
using ReaGE.DevTools;
using RszTool;

public class PfbConverter : SceneRszAssetConverter<PrefabResource, PfbFile, PrefabNode>
{
    public override PfbFile CreateFile(FileHandler fileHandler)
    {
        return new PfbFile(Convert.FileOption, fileHandler);
    }

    public override bool LoadFile(PfbFile file)
    {
        if (!file.Read()) return false;
        file.SetupGameObjects();
        return true;
    }

    public override Task<bool> Export(PrefabNode source, PfbFile file)
    {
        var fileOption = TypeCache.CreateRszFileOptions(Config);

        source.PreExport();
        foreach (var go in source.AllChildrenIncludingSelf) {
            AddMissingResourceInfos(go, source);
        }

        StoreResources(source.Resources, file.ResourceInfoList, true);
        file.RSZ.ClearInstances();

        AddGameObject(source, file.RSZ, file, -1);
        SetupPfbGameObjectReferences(file, source, source);
        SetupGameObjectReferenceGuids(source, source);
        return Task.FromResult(true);
    }

    protected RszInstance AddGameObject(GameObject obj, RSZFile rsz, PfbFile file, int parentObjectId)
    {
        var instance = ExportREObject(obj.Data!, rsz, FileOption, file);

        rsz.AddToObjectTable(instance);
        AddPfbGameObject(instance.ObjectTableIndex, file, obj.Components.Count, parentObjectId);

        foreach (var comp in obj.Components) {
            var typeinfo = comp.TypeInfo;
            rsz.AddToObjectTable(ExportREObject(comp, rsz, FileOption, file));
        }

        foreach (var child in obj.Children) {
            AddGameObject(child, rsz, file, instance.ObjectTableIndex);
        }

        return instance;
    }

    private void SetupPfbGameObjectReferences(PfbFile pfb, GameObject gameobj, PrefabNode root)
    {
        foreach (var comp in gameobj.Components) {
            RecurseSetupPfbGameObjectReferences(pfb, comp, comp, root);
        }

        foreach (var child in gameobj.Children) {
            SetupPfbGameObjectReferences(pfb, child, root);
        }
    }

    private void RecurseSetupPfbGameObjectReferences(PfbFile pfb, REObject data, REComponent component, PrefabNode root, int arrayIndex = 0)
    {
        Dictionary<string, PrefabGameObjectRefProperty>? propInfoDict = null;
        foreach (var field in data.TypeInfo.Fields) {
            if (field.RszField.type == RszFieldType.GameObjectRef) {
                if (field.RszField.array) {
                    GD.PrintErr("GameObjectRef array export currently unsupported!! " + component.Path);
                } else {
                    if (data.TryGetFieldValue(field, out var path) && path.As<GameObjectRef>() is GameObjectRef objref && !objref.IsEmpty) {
                        var target = objref.Resolve(component.GameObject);
                        if (target == null) {
                            GD.Print("Invalid pfb node path reference " + objref + " at " + component.Path);
                            continue;
                        }

                        propInfoDict ??= TypeCache.GetClassInfo(root.Game, data.Classname!).PfbRefs;
                        if (!propInfoDict.TryGetValue(field.SerializedName, out var propInfo)) {
                            GD.PrintErr("Found undeclared GameObjectRef property " + field.SerializedName);
                            continue;
                        }

                        if (!exportedInstances.TryGetValue(data, out var dataInst) || !exportedInstances.TryGetValue(target.Data!, out var targetInst)) {
                            GD.PrintErr("Could not resolve GameObjectRef instances");
                            continue;
                        }

                        if (dataInst.ObjectTableIndex == -1) {
                            pfb.RSZ!.AddToObjectTable(dataInst);
                        }

                        var refEntry = new StructModel<RszTool.Pfb.PfbGameObjectRefInfo>() {
                            Data = new RszTool.Pfb.PfbGameObjectRefInfo() {
                                objectId = (uint)dataInst.ObjectTableIndex,
                                arrayIndex = arrayIndex,
                                propertyId = propInfo.PropertyId,
                                targetId = (uint)targetInst.ObjectTableIndex,
                            }
                        };

                        // propertyId seems to be some 16bit + 16bit value
                        // first 2 bytes would seem like a field index, but I'm not finding direct correlation between the fields and indexes
                        // the second 2 bytes seem to be a property type
                        // judging from ch000000_00 pfb, type 2 = "Exported ref" (source objectId instance added to the object info list)
                        // type 4 = something else (default?)
                        // could be some flag thing
                        pfb.GameObjectRefInfoList.Add(refEntry);
                    }
                }
            } else if (field.RszField.type == RszFieldType.Object) {
                if (data.TryGetFieldValue(field, out var child)) {
                    if (field.RszField.array) {
                        if (child.AsGodotArray<REObject>() is Godot.Collections.Array<REObject> children) {
                            int i = 0;
                            foreach (var childObj in children) {
                                if (childObj != null) {
                                    RecurseSetupPfbGameObjectReferences(pfb, childObj, component, root, i++);
                                }
                            }
                        }
                    } else {
                        if (child.VariantType != Variant.Type.Nil && child.As<REObject>() is REObject childObj) {
                            RecurseSetupPfbGameObjectReferences(pfb, childObj, component, root);
                        }
                    }
                }
            }
        }
    }

    private static void AddPfbGameObject(int objectId, PfbFile file, int componentCount, int parentId)
    {
        file.GameObjectInfoList.Add(new StructModel<RszTool.Pfb.PfbGameObjectInfo>() {
            Data = new RszTool.Pfb.PfbGameObjectInfo() {
                objectId = objectId,
                parentId = parentId,
                componentCount = componentCount,
            }
        });
    }

    public override async Task<bool> Import(PfbFile file, PrefabNode target)
    {
        GenerateResources(target, file.ResourceInfoList);
        var batch = Convert.CreatePrefabBatch(target, target.Asset!.AssetFilename);

        TypeCache.StoreInferredRszTypes(file.RSZ, Config);

        if (Convert.Options.prefabs == RszImportType.ForceReimport) {
            target.Clear();
        }

        target.Prefab = target.Asset.AssetFilename;
        var rootGOs = file.GameObjects!.OrderBy(o => o.Instance!.Index);
        Debug.Assert(rootGOs.Count() <= 1, "WTF Capcom?? Guess we doing multiple PFB roots now");
        foreach (var gameObj in rootGOs) {
            target.OriginalName = gameObj.Name ?? target.Name;
            PrepareGameObjectBatch(gameObj, batch, target);
            await batch.Await(Convert);
        }

        foreach (var go in target.AllChildrenIncludingSelf) {
            foreach (var comp in go.Components) {
                ReconstructPfbGameObjectRefs(file, comp, comp, target);
            }
        }
        return true;
    }

    private GameObjectRef? ResolveGameObjectRef(PfbFile file, REField field, REObject obj, RszInstance instance, REComponent component, GameObject root, int arrayIndex)
    {
        // god help me...
        Debug.Assert(instance != null);
        int idx = instance.ObjectTableIndex;

        var fieldRefs = file.GameObjectRefInfoList.Where(rr => rr.Data.objectId == idx && rr.Data.arrayIndex == arrayIndex);
        if (!fieldRefs.Any()) return null;

        var cache = TypeCache.GetClassInfo(Game, obj.Classname!);
        var propInfoDict = cache.PfbRefs;
        if (!propInfoDict.TryGetValue(field.SerializedName, out var propInfo)) {
            GameObjectRefResolver.CheckInstances(Game, file);
            if (!propInfoDict.TryGetValue(field.SerializedName, out propInfo)) {
                // if any refs from this object do not have a known property Id; this way we only print error if we actually found an unmapped ref
                if (file.GameObjectRefInfoList.Any(info => info.Data.objectId == idx
                    && !propInfoDict.Values.Any(entry => entry.PropertyId == info.Data.propertyId))) {
                    ErrorLog($"Found unknown GameObjectRef property {field.SerializedName} in class {obj.Classname}. It might not be imported correctly.");
                }
                return default;
            }
        }

        var objref = fieldRefs.FirstOrDefault(rr => rr.Data.propertyId == propInfo.PropertyId);
        if (objref == null) {
            ErrorLog("Could not match GameObjectRef field ref");
            return default;
        }

        var targetInstance = file.RSZ?.ObjectList[(int)objref.Data.targetId];
        if (targetInstance == null) {
            ErrorLog("GameObjectRef target object not found");
            return default;
        }

        if (!importedObjects.TryGetValue(targetInstance, out var targetGameobjData)) {
            ErrorLog("Referenced game object was not imported");
            return default;
        }

        var targetGameobj = root.AllChildrenIncludingSelf.FirstOrDefault(x => x.Data == targetGameobjData);
        if (targetGameobj == null) {
            ErrorLog("Could not find actual gameobject instance");
            return default;
        }

        return new GameObjectRef(targetGameobj.ObjectGuid == Guid.Empty ? (Guid)instance.Values[field.FieldIndex] : targetGameobj.ObjectGuid, component.GameObject.GetPathTo(targetGameobj));
    }

    private void ReconstructPfbGameObjectRefs(PfbFile file, REObject obj, REComponent component, GameObject root, int arrayIndex = 0)
    {
        RszInstance? instance = null;
        foreach (var field in obj.TypeInfo.Fields) {
            if (field.RszField.type == RszFieldType.GameObjectRef) {
                instance ??= objectSourceInstances[obj];
                if (field.RszField.array) {
                    var indices = (IList<object>)instance.Values[field.FieldIndex];
                    var paths = new Godot.Collections.Array<GameObjectRef>();
                    for (int i = 0; i < indices.Count; ++i) {
                        var refval = ResolveGameObjectRef(file, field, obj, instance, component, root, i);
                        if (refval == null && (Guid)indices[i] != Guid.Empty) {
                            ErrorLog($"Couldn't resolve pfb GameObjectRef node path field {field.SerializedName}[{i}] for {component.Path}");
                        }
                        paths.Add(refval ?? new GameObjectRef());
                    }
                    obj.SetField(field, paths);
                } else {
                    var refval = ResolveGameObjectRef(file, field, obj, instance, component, root, 0);
                    if (refval == null && (Guid)instance.Values[field.FieldIndex] != Guid.Empty) {
                        ErrorLog($"Couldn't resolve pfb GameObjectRef node path in field {field.SerializedName} for {component.Path}");
                    }
                    obj.SetField(field, refval ?? new GameObjectRef());
                }
            } else if (field.RszField.type is RszFieldType.Object) {
                if (!obj.TryGetFieldValue(field, out var child)) continue;

                if (field.RszField.array) {
                    if (child.AsGodotArray<REObject>() is Godot.Collections.Array<REObject> children) {
                        int i = 0;
                        foreach (var childObj in children) {
                            if (childObj != null) {
                                ReconstructPfbGameObjectRefs(file, childObj, component, root, i++);
                            }
                        }
                    }
                } else {
                    if (child.As<REObject>() is REObject childObj) {
                        ReconstructPfbGameObjectRefs(file, childObj, component, root);
                    }
                }
            }
        }
    }

}
