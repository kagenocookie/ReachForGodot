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

    public static bool ExportFile(IRszContainerNode resource, string exportBasepath)
    {
        var outputPath = ResolveExportPath(exportBasepath, resource.Asset);
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

        using var sourceFile = new UserFile(fileOption, new FileHandler(Importer.ResolveSourceFilePath(userdata.Asset?.AssetFilename!, config)!));
        sourceFile.Read();
        sourceFile.RebuildInfoTable();

        using var file = new UserFile(fileOption, new FileHandler(outputFile));
        SetResources(userdata.Resources, file.ResourceInfoList, fileOption);
        file.RSZ = new RSZFile(fileOption, sourceFile.FileHandler);
        file.RSZ.ClearInstances();
        file.RSZ.InstanceInfoList.Add(new InstanceInfo());
        file.RSZ.Header.Data.version = 16; // TODO what's this version do?
        ConstructObjectInstances(userdata, file.RSZ, fileOption);
        file.RSZ.AddToObjectTable(file.RSZ.InstanceList.Last());
        var success = file.Save();

        if (!success && File.Exists(outputFile) && new FileInfo(outputFile).Length == 0) {
            File.Delete(outputFile);
        }

        return success;
    }

    private static void SetResources(REResource[]? resources, List<ResourceInfo> list, RszFileOption fileOption)
    {
        if (resources != null) {
            foreach (var res in resources) {
                // var normalizedFilename = res.Asset?.AssetFilename
                list.Add(ResourceInfo.Create(res.Asset?.NormalizedFilepath, fileOption.Version));
            }
        }
    }

    private static void ConstructObjectInstances(REObject target, RSZFile rsz, RszFileOption fileOption)
    {
        if (string.IsNullOrEmpty(target.Classname)) {
            throw new ArgumentNullException("Missing root REObject classname " + target.Classname);
        }
        int i = 0;
        var values = new object[target.TypeInfo.Fields.Length];
        foreach (var field in target.TypeInfo.Fields) {
            if (target.TryGetFieldValue(field, out var value)) {
                if (field.RszField.type is RszFieldType.Object or RszFieldType.UserData) {
                    if (field.RszField.array) {
                        var array = value.AsGodotArray<REObject>() ?? throw new Exception("Unhandled rsz object array type");
                        var array_refs = new object[array.Count];
                        for (int arr_idx = 0; arr_idx < array.Count; ++arr_idx) {
                            ConstructObjectInstances(array[arr_idx], rsz, fileOption);
                            array_refs[arr_idx] = rsz.InstanceList.Count - 1;
                        }
                        values[i++] = array_refs;
                    } else {
                        var obj = value.As<REObject>() ?? throw new Exception("Unhandled rsz object array type");
                        // TODO what about non-REObjects? (scn, pfb refs?)
                        ConstructObjectInstances(obj, rsz, fileOption);
                        values[i++] = rsz.InstanceList.Count;
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

        var rszClass = target.TypeInfo.RszClass;
        RszInstance instance;
        if (target is UserdataResource userdata) {
            // TODO extra userdata stuff?
            instance = new RszInstance(rszClass, rsz.InstanceList.Count, null, values);
        } else {
            instance = new RszInstance(rszClass, rsz.InstanceList.Count, null, values);
        }

        rsz.InstanceInfoList.Add(new InstanceInfo() { ClassName = rszClass.name, CRC = rszClass.crc, typeId = rszClass.typeId });
        rsz.InstanceList.Add(instance);
    }
}