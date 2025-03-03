using System.Diagnostics;
using System.Management;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using RszTool;

namespace RGE;

public class Exporter
{
    private static readonly Dictionary<REObject, RszInstance> exportedInstances = new();

    public static string? ResolveExportPath(string? basePath, string? assetPath, SupportedGame game)
    {
        if (!Path.IsPathRooted(assetPath)) {
            if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(basePath)) {
                return null;
            }

            assetPath = Path.Combine(basePath, assetPath);
        }

        var config = ReachForGodot.GetAssetConfig(game) ?? throw new Exception("Missing config for game " + game);
        return PathUtils.AppendFileVersion(assetPath, config);
    }

    public static bool Export(IRszContainerNode resource, string exportBasepath)
    {
        var outputPath = ResolveExportPath(exportBasepath, resource.Asset!.AssetFilename, resource.Game);
        if (string.IsNullOrEmpty(outputPath)) {
            GD.PrintErr("Invalid empty export filepath");
            return false;
        }

        exportedInstances.Clear();
        if (resource is REResource reres) {
            switch (reres.ResourceType) {
                case RESupportedFileFormats.Userdata:
                    return ExportUserdata((UserdataResource)reres, outputPath);
                default:
                    GD.PrintErr("Currently unsupported export for resource type " + reres.ResourceType);
                    break;
            }
        } else if (resource is PrefabNode pfb) {
            return ExportPrefab(pfb, outputPath);
        } else if (resource is SceneFolder scn) {
            return ExportScene(scn, outputPath);
        } else {
            GD.PrintErr("Currently unsupported export for file type " + resource.GetType());
        }

        return false;
    }

    private static bool ExportUserdata(UserdataResource userdata, string outputFile)
    {
        Directory.CreateDirectory(outputFile.GetBaseDir());
        AssetConfig config = ReachForGodot.GetAssetConfig(userdata.Game);
        var fileOption = TypeCache.CreateRszFileOptions(config);

        var handler = new FileHandler(PathUtils.ResolveSourceFilePath(userdata.Asset?.AssetFilename!, config)!);
        // var sourceFile = new UserFile(fileOption, new FileHandler(Importer.ResolveSourceFilePath(userdata.Asset?.AssetFilename!, config)!));
        // sourceFile.Read();

        using var file = new UserFile(fileOption, new FileHandler(outputFile));
        SetResources(userdata.Resources, file.ResourceInfoList, fileOption);
        file.RSZ.ClearInstances();

        var rootInstance = ConstructObjectInstances(userdata, file.RSZ, fileOption, file);
        file.RSZ.AddToObjectTable(file.RSZ.InstanceList[rootInstance]);
        var success = file.Save();

        if (!success && File.Exists(outputFile) && new FileInfo(outputFile).Length == 0) {
            File.Delete(outputFile);
        }

        return success;
    }

    private static bool ExportScene(SceneFolder root, string outputFile)
    {
        Directory.CreateDirectory(outputFile.GetBaseDir());
        AssetConfig config = ReachForGodot.GetAssetConfig(root.Game);
        var fileOption = TypeCache.CreateRszFileOptions(config);

        // var handler = new FileHandler(PathUtils.ResolveSourceFilePath(root.Asset?.AssetFilename!, config)!);
        // var sourceFile = new ScnFile(fileOption, handler);
        // sourceFile.Read();
        // sourceFile.SetupGameObjects();

        root.PreExport();

        using var file = new ScnFile(fileOption, new FileHandler(outputFile));
        SetResources(root.Resources, file.ResourceInfoList, fileOption);
        file.RSZ.ClearInstances();

        foreach (var go in root.ChildObjects) {
            if (go is PrefabNode pfbGo) {
                GD.PrintErr("TODO: PFB sourced game object inside SCN");
                file.PrefabInfoList.Add(new ScnFile.PrefabInfo() {
                    Path = pfbGo.Asset!.AssetFilename,
                    parentId = 0,
                });
                continue;
            }

            AddGameObject(go, file.RSZ, file, fileOption, -1);
        }

        foreach (var folder in root.Subfolders) {
            AddFolder(folder, file, fileOption, -1);
        }

        var success = file.Save();
        if (!success && File.Exists(outputFile) && new FileInfo(outputFile).Length == 0) {
            File.Delete(outputFile);
        }

        return success;
    }

    private static bool ExportPrefab(PrefabNode root, string outputFile)
    {
        Directory.CreateDirectory(outputFile.GetBaseDir());
        AssetConfig config = ReachForGodot.GetAssetConfig(root.Game);
        var fileOption = TypeCache.CreateRszFileOptions(config);

        // var handler = new FileHandler(PathUtils.ResolveSourceFilePath(root.Asset?.AssetFilename!, config)!);
        // var sourceFile = new PfbFile(fileOption, handler);
        // sourceFile.Read();
        // sourceFile.SetupGameObjects();

        foreach (var go in root.Children) {
            go.PreExport();
        }

        using var file = new PfbFile(fileOption, new FileHandler(outputFile));
        SetResources(root.Resources, file.ResourceInfoList, fileOption);
        file.RSZ.ClearInstances();

        AddGameObject(root, file.RSZ, file, fileOption, -1);
        SetupGameObjectReferences(file, root, root);
        var success = file.Save();

        if (!success && File.Exists(outputFile) && new FileInfo(outputFile).Length == 0) {
            File.Delete(outputFile);
        }

        return success;
    }

    private static void AddFolder(SceneFolder folder, ScnFile file, RszFileOption fileOption, int parentFolderId)
    {
        var folderCls = file.RszParser.GetRSZClass("via.Folder") ?? throw new Exception("Could not get folder rsz class");
        var instanceId = file.RSZ.InstanceInfoList.Count;
        var folderInstance = new RszInstance(folderCls, instanceId);

        file.RSZ.InstanceInfoList.Add(new InstanceInfo() { typeId = folderCls.typeId, CRC = folderCls.crc, ClassName = "via.Folder" });
        file.RSZ.InstanceList.Add(folderInstance);
        file.RSZ.AddToObjectTable(folderInstance);

        file.FolderInfoList.Add(new StructModel<ScnFile.FolderInfo>() { Data = new ScnFile.FolderInfo() {
            objectId = folderInstance.ObjectTableIndex,
            parentId = parentFolderId,
        } });

        var linkedSceneFilepath = folder is SceneFolderProxy proxy && proxy.Asset != null ? proxy.Asset.AssetFilename : string.Empty;
        folderInstance.Values[0] = folder.Name.ToString();
        folderInstance.Values[1] = string.Empty; // tags?
        folderInstance.Values[2] = (byte)1; // ?
        folderInstance.Values[3] = (byte)1; // ?
        folderInstance.Values[4] = (byte)0; // ?
        folderInstance.Values[5] = linkedSceneFilepath;
        folderInstance.Values[6] = new byte[18]; // ?

        foreach (var go in folder.ChildObjects) {
            if (go is PrefabNode pfbGo) {
                GD.PrintErr("TODO: PFB sourced game object inside SCN");
                continue;
            }

            AddGameObject(go, file.RSZ, file, fileOption, folderInstance.ObjectTableIndex);
        }

        if (string.IsNullOrEmpty(linkedSceneFilepath)) {
            foreach (var sub in folder.Subfolders) {
                AddFolder(sub, file, fileOption, folderInstance.ObjectTableIndex);
            }
        }
    }

    private static int AddGameObject(REGameObject obj, RSZFile rsz, BaseRszFile container, RszFileOption fileOption, int parentObjectId)
    {
        var instanceId = ConstructObjectInstances(obj.Data!, rsz, fileOption, container);
        var instance = rsz.InstanceList[instanceId];

        rsz.AddToObjectTable(instance);
        if (container is ScnFile scn) {
            AddScnGameObject(instance.ObjectTableIndex, scn, obj, parentObjectId);
        } else if (container is PfbFile pfb) {
            AddPfbGameObject(instance.ObjectTableIndex, pfb, obj.Components.Count, parentObjectId);
        }

        foreach (var comp in obj.Components) {
            var typeinfo = comp.TypeInfo;
            int dataIndex = ConstructObjectInstances(comp, rsz, fileOption, container);
            rsz.AddToObjectTable(rsz.InstanceList[dataIndex]);
        }

        foreach (var child in obj.Children) {
            AddGameObject(child, rsz, container, fileOption, instance.ObjectTableIndex);
        }

        return instanceId;
    }

    private static void SetupGameObjectReferences(PfbFile pfb, REGameObject gameobj, PrefabNode root)
    {
        foreach (var comp in gameobj.Components) {
            RecurseSetupGameObjectReferences(pfb, comp, comp, root);
        }

        foreach (var child in gameobj.Children) {
            SetupGameObjectReferences(pfb, child, root);
        }
    }

    private static void RecurseSetupGameObjectReferences(PfbFile pfb, REObject data, REComponent component, PrefabNode root, int arrayIndex = 0)
    {
        Dictionary<string, PrefabGameObjectRefProperty>? propInfoDict = null;
        var ti = data.TypeInfo;
        foreach (var field in ti.Fields) {
            if (field.RszField.type == RszFieldType.GameObjectRef) {
                if (field.RszField.array) {
                    GD.PrintErr("GameObjectRef array export currently unsupported!! " + root.GetPathTo(component.GameObject));
                } else {
                    if (data.TryGetFieldValue(field, out var path) && path.AsNodePath() is NodePath nodepath && !nodepath.IsEmpty) {
                        // GD.Print($"Found GameObjectRef {component.GameObject}/{component} => {field.SerializedName} {nodepath}");
                        var target = component.GameObject.GetNode(nodepath) as REGameObject;
                        if (target == null) {
                            GD.Print("Invalid node path reference " + nodepath + " at " + root.GetPathTo(component.GameObject));
                            continue;
                        }

                        propInfoDict ??= TypeCache.GetData(root.Game, data.Classname!).PfbRefs;
                        if (!propInfoDict.TryGetValue(field.SerializedName, out var propInfo)) {
                            GD.PrintErr("Found undeclared GameObjectRef property " + field.SerializedName);
                            continue;
                        }

                        if (!exportedInstances.TryGetValue(data, out var dataInst) || !exportedInstances.TryGetValue(target.Data!, out var targetInst)) {
                            GD.PrintErr("Could not resolve GameObjectRef instances");
                            continue;
                        }

                        // if (propInfo.AddToObjectTable && dataInst.ObjectTableIndex == -1) {
                        if (dataInst.ObjectTableIndex == -1) {
                            pfb.RSZ!.AddToObjectTable(dataInst);
                        }

                        var refEntry = new StructModel<PfbFile.GameObjectRefInfo>() {
                            Data = new PfbFile.GameObjectRefInfo() {
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
                                    RecurseSetupGameObjectReferences(pfb, childObj, component, root, i++);
                                }
                            }
                        }
                    } else {
                        if (child.VariantType != Variant.Type.Nil && child.As<REObject>() is REObject childObj) {
                            RecurseSetupGameObjectReferences(pfb, childObj, component, root);
                        }
                    }
                }
            }
        }
    }

    private static void AddPfbGameObject(int objectId, PfbFile file, int componentCount, int parentId)
    {
        file.GameObjectInfoList.Add(new StructModel<PfbFile.GameObjectInfo>() {
            Data = new PfbFile.GameObjectInfo() {
                objectId = objectId,
                parentId = parentId,
                componentCount = componentCount,
            }
        });
    }

    private static void AddScnGameObject(int objectId, ScnFile file, REGameObject gameObject, int parentId)
    {
        var pfbIndex = -1;
        if (gameObject is PrefabNode pfbNode && pfbNode.Asset?.IsEmpty == false) {
            pfbIndex = file.PrefabInfoList.FindIndex(pfb => pfb.Path == pfbNode.Asset.AssetFilename);
            if (pfbIndex == -1) {
                file.PrefabInfoList.Add(new ScnFile.PrefabInfo() {
                    parentId = 0,
                    Path = pfbNode.Asset.AssetFilename,
                });
            }
        }

        file.GameObjectInfoList.Add(new StructModel<ScnFile.GameObjectInfo>() {
            Data = new ScnFile.GameObjectInfo() {
                objectId = objectId,
                parentId = parentId,
                componentCount = (short)gameObject.Components.Count,
                guid = Guid.TryParse(gameObject.Uuid, out var guid) ? guid : Guid.Empty,
                prefabId = pfbIndex,
            }
        });
    }

    private static void SetResources(REResource[]? resources, List<ResourceInfo> list, RszFileOption fileOption)
    {
        if (resources != null) {
            foreach (var res in resources) {
                list.Add(new ResourceInfo(fileOption.Version) { Path = res.Asset?.AssetFilename });
            }
        }
    }

    private static int ConstructObjectInstances(REObject target, RSZFile rsz, RszFileOption fileOption, BaseRszFile container)
    {
        if (exportedInstances.TryGetValue(target, out var instance)) {
            return instance.Index;
        }
        int i = 0;
        RszClass rszClass;
        if (target is UserdataResource userdata) {
            rszClass = target.TypeInfo.RszClass;
            if (string.IsNullOrEmpty(target.Classname)) {
                userdata.Reimport();
                if (string.IsNullOrEmpty(target.Classname)) {
                    throw new ArgumentNullException("Missing root REObject classname " + target.Classname);
                }
            }
            var path = userdata.Asset!.AssetFilename;
            RSZUserDataInfo? userDataInfo = rsz.RSZUserDataInfoList.FirstOrDefault(u => (u as RSZUserDataInfo)?.Path == path) as RSZUserDataInfo;
            if (userDataInfo == null) {
                var fileUserdataList = (container as PfbFile)?.UserdataInfoList ?? (container as ScnFile)?.UserdataInfoList;
                fileUserdataList!.Add(new UserdataInfo() { CRC = rszClass.crc, typeId = rszClass.typeId, Path = path });
                userDataInfo = new RSZUserDataInfo() { typeId = rszClass.typeId, Path = path, instanceId = rsz.InstanceList.Count };
                rsz.RSZUserDataInfoList.Add(userDataInfo);
            }

            instance = new RszInstance(rszClass, userDataInfo.instanceId, userDataInfo, []);
        } else {
            if (string.IsNullOrEmpty(target.Classname)) {
                throw new ArgumentNullException("Missing root REObject classname " + target.Classname);
            }
            rszClass = target.TypeInfo.RszClass;
            instance = new RszInstance(rszClass, rsz.InstanceList.Count, null, new object[target.TypeInfo.Fields.Length]);

            var values = instance.Values;
            foreach (var field in target.TypeInfo.Fields) {
                if (!target.TryGetFieldValue(field, out var value)) {
                    values[i++] = RszInstance.CreateNormalObject(field.RszField);
                    continue;
                }

                switch (field.RszField.type) {
                    case RszFieldType.Object:
                    case RszFieldType.UserData:
                        if (field.RszField.array) {
                            var array = value.AsGodotArray<REObject>() ?? throw new Exception("Unhandled rsz object array type");
                            var array_refs = new object[array.Count];
                            for (int arr_idx = 0; arr_idx < array.Count; ++arr_idx) {
                                var val = array[arr_idx];
                                array_refs[arr_idx] = val == null ? 0 : (object)ConstructObjectInstances(array[arr_idx], rsz, fileOption, container);
                            }
                            values[i++] = array_refs;
                        } else if (value.VariantType == Variant.Type.Nil) {
                            values[i++] = 0; // index 0 is the null instance entry
                        } else {
                            var obj = value.As<REObject>() ?? throw new Exception("Unhandled rsz object array type");
                            values[i++] = ConstructObjectInstances(obj, rsz, fileOption, container);
                        }
                        break;
                    case RszFieldType.Resource:
                        if (field.RszField.array) {
                            values[i++] = value.AsGodotArray<REResource>().Select(obj => obj?.Asset?.AssetFilename ?? string.Empty).ToArray();
                        } else {
                            values[i++] = value.As<REResource?>()?.Asset?.AssetFilename ?? string.Empty;
                        }
                        break;
                    case RszFieldType.GameObjectRef:
                        // TODO handle guids in scenes
                        if (field.RszField.array) {
                            values[i++] = value.AsGodotArray<NodePath>().Select(p => (object)Guid.Empty).ToArray();
                        } else {
                            values[i++] = Guid.Empty;
                        }
                        break;
                    default:
                        var converted = RszTypeConverter.ToRszStruct(value, field);
                        values[i++] = converted ?? RszInstance.CreateNormalObject(field.RszField);
                        break;
                }
            }
        }

        instance.Index = rsz.InstanceList.Count;
        rsz.InstanceInfoList.Add(new InstanceInfo() { ClassName = rszClass.name, CRC = rszClass.crc, typeId = rszClass.typeId });
        rsz.InstanceList.Add(instance);
        exportedInstances[target] = instance;
        return instance.Index;
    }
}