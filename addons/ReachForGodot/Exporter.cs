using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using RszTool;

namespace RGE;

public class Exporter
{
    public static string? ResolveExportPath(ExportPathSetting? basePath, AssetReference? asset)
        => ResolveExportPath(basePath?.path, asset?.AssetFilename);
    public static string? ResolveExportPath(ExportPathSetting? basePath, string? filepath)
        => ResolveExportPath(basePath?.path, filepath);

    public static string? ResolveExportPath(string? basePath, SupportedGame game)
        => ResolveExportPath(basePath, ReachForGodot.GetChunkPath(game));
    public static string? ResolveExportPath(string? basePath, string? filepath)
    {
        if (Path.IsPathRooted(filepath)) {
            return filepath;
        }

        if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(filepath)) {
            return null;
        }

        return Path.Combine(basePath, filepath);
    }

    public static bool Export(IRszContainerNode resource, string exportBasepath)
    {
        var outputPath = ResolveExportPath(exportBasepath, resource.Asset ?? throw new Exception("Resource asset is null"));
        if (string.IsNullOrEmpty(outputPath)) {
            GD.PrintErr("Invalid empty export filepath");
            return false;
        }

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

        var handler = new FileHandler(Importer.ResolveSourceFilePath(userdata.Asset?.AssetFilename!, config)!);
        // var sourceFile = new UserFile(fileOption, new FileHandler(Importer.ResolveSourceFilePath(userdata.Asset?.AssetFilename!, config)!));
        // sourceFile.Read();

        using var file = new UserFile(fileOption, new FileHandler(outputFile));
        SetResources(userdata.Resources, file.ResourceInfoList, fileOption);
        file.RSZ = new RSZFile(fileOption, handler);
        file.RSZ.ClearInstances();
        file.RSZ.InstanceInfoList.Add(new InstanceInfo());
        file.RSZ.Header.Data.version = 16; // TODO what's this version do?
        var rootInstance = ConstructObjectInstances(userdata, file.RSZ, fileOption, file);
        file.RSZ.AddToObjectTable(file.RSZ.InstanceList[rootInstance]);
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

        var handler = new FileHandler(Importer.ResolveSourceFilePath(root.Asset?.AssetFilename!, config)!);
        // var sourceFile = new PfbFile(fileOption, handler);
        // sourceFile.Read();
        // sourceFile.SetupGameObjects();

        foreach (var go in root.AllChildrenIncludingSelf) {
            foreach (var comp in go.Components) {
                comp.PreExport();
            }
        }

        using var file = new PfbFile(fileOption, new FileHandler(outputFile));
        SetResources(root.Resources, file.ResourceInfoList, fileOption);
        file.RSZ = new RSZFile(fileOption, handler);
        file.RSZ.Header.Data.version = 16; // TODO what's this version do?
        file.RSZ.ClearInstances();
        file.RSZ.InstanceInfoList.Add(new InstanceInfo());

        foreach (var gameobj in root.FindChildrenByType<REGameObject>()) {
            AddGameObject(gameobj, file.RSZ, file, fileOption);
        }
        foreach (var gameobj in root.FindChildrenByType<REGameObject>()) {
            SetupGameObjectReferences(file, gameobj);
        }
        var success = file.Save();

        if (!success && File.Exists(outputFile) && new FileInfo(outputFile).Length == 0) {
            File.Delete(outputFile);
        }

        return success;
    }

    private static int AddGameObject(REGameObject obj, RSZFile rsz, BaseRszFile container, RszFileOption fileOption)
    {
        var gameobjectIndex = rsz.InstanceInfoList.Count;
        var goClass = rsz.RszParser.GetRSZClass("via.GameObject")!;
        var instance = obj.GetData(goClass);
        rsz.InstanceInfoList.Add(new InstanceInfo() { ClassName = goClass.name, CRC = goClass.crc, typeId = goClass.typeId });
        rsz.InstanceList.Add(instance);

        if (container is ScnFile scn) {
            AddScnGameObject(gameobjectIndex, scn, obj.ComponentContainer?.GetChildCount() ?? 0);
        } else if (container is PfbFile pfb) {
            AddPfbGameObject(gameobjectIndex, pfb, obj.ComponentContainer?.GetChildCount() ?? 0);
        }

        foreach (var comp in obj.Components) {
            var typeinfo = comp.Data!.TypeInfo;
            int dataIndex = ConstructObjectInstances(comp.Data!, rsz, fileOption, container);
            rsz.ObjectList.Add(rsz.InstanceList[dataIndex]);

        }

        foreach (var child in obj.Children) {
            AddGameObject(child, rsz, container, fileOption);
        }
        rsz.AddToObjectTable(instance);

        return gameobjectIndex;
    }

    private static void AddPfbGameObject(int instanceId, PfbFile file, int componentCount)
    {
        var objectId = file.GameObjectInfoList.Count;
        file.GameObjectInfoList.Add(new StructModel<PfbFile.GameObjectInfo>() {
            Data = new PfbFile.GameObjectInfo() {
                objectId = objectId,
                parentId = -1,
                componentCount = componentCount,
            }
        });
        file.RSZ!.ObjectList.Add(file.RSZ!.InstanceList[instanceId]);
    }

    private static void SetupGameObjectReferences(PfbFile pfb, REGameObject gameobj)
    {
        foreach (var comp in gameobj.Components) {
            var ti = comp.Data!.TypeInfo;
            if (ti.GameObjectRefFields.Length == 0) continue;

            foreach (var objref in ti.GameObjectRefFields) {
                if (comp.Data.TryGetFieldValue(ti.Fields[objref], out var uuidString) && uuidString.AsString() != Guid.Empty.ToString()) {
                    // TODO
                    // pfb.GameObjectRefInfoList.Add(new StructModel<PfbFile.GameObjectRefInfo>() {
                    //     Data = new PfbFile.GameObjectRefInfo() {
                    //         arrayIndex = 0,
                    //         objectId = (uint)instanceId,
                    //     }
                    // });
                }
            }
        }

        foreach (var child in gameobj.Children) {
            SetupGameObjectReferences(pfb, child);
        }
    }

    private static void AddScnGameObject(int instanceId, ScnFile file, int componentCount)
    {
        file.GameObjectInfoList.Add(new StructModel<ScnFile.GameObjectInfo>() {
            Data = new ScnFile.GameObjectInfo() {
                objectId = 1,
                parentId = -1,
                componentCount = (short)componentCount,
            }
        });
        throw new NotImplementedException();
    }

    private static void SetResources(REResource[]? resources, List<ResourceInfo> list, RszFileOption fileOption)
    {
        if (resources != null) {
            foreach (var res in resources) {
                list.Add(ResourceInfo.Create(res.Asset?.NormalizedFilepath, fileOption.Version));
            }
        }
    }

    private static int ConstructObjectInstances(REObject target, RSZFile rsz, RszFileOption fileOption, BaseRszFile container)
    {
        int i = 0;
        RszClass rszClass;
        RszInstance instance;
        if (target is UserdataResource userdata) {
            rszClass = target.TypeInfo.RszClass;
            if (string.IsNullOrEmpty(target.Classname)) {
                userdata.Reimport();
                if (string.IsNullOrEmpty(target.Classname)) {
                    throw new ArgumentNullException("Missing root REObject classname " + target.Classname);
                }
            }
            var path = userdata.Asset!.NormalizedFilepath;
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
                if (target.TryGetFieldValue(field, out var value)) {
                    if (field.RszField.type is RszFieldType.Object or RszFieldType.UserData) {
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
                    } else if (field.RszField.type is RszFieldType.Resource) {
                        if (field.RszField.array) {
                            values[i++] = value.AsGodotArray<REResource>().Select(obj => obj?.Asset?.NormalizedFilepath ?? string.Empty).ToArray();
                        } else {
                            values[i++] = value.As<REResource?>()?.Asset?.AssetFilename ?? string.Empty;
                        }
                    } else {
                        var converted = RszTypeConverter.ToRszStruct(value, field);
                        values[i++] = converted ?? RszInstance.CreateNormalObject(field.RszField);
                    }
                } else {
                    values[i++] = RszInstance.CreateNormalObject(field.RszField);
                }
            }
        }

        rsz.InstanceInfoList.Add(new InstanceInfo() { ClassName = rszClass.name, CRC = rszClass.crc, typeId = rszClass.typeId });
        rsz.InstanceList.Add(instance);
        return instance.Index;
    }
}