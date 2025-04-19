namespace ReaGE.DevTools;

using System;
using System.Text;
using Godot;
using RszTool;

public sealed class ResourceFieldFinder : IDisposable
{
    private Dictionary<SupportedGame, ResourceList> dict = new();

    private sealed class ResourceList
    {
        public HashSet<(string cls, string field, string ext, bool? shouldBeInResourceList)> resources = new();
        public HashSet<(string cls, string field)> nonResources = new();
    }

    public void DumpFindings(string? suffix)
    {
        foreach (var (game, list) in dict) {
            var sb = new StringBuilder();
            foreach (var (cls, field, ext, inList) in list.resources.OrderBy(a => (a.cls, a.field))) {
                sb.Append("ext:").Append(ext).Append(" class:").Append(cls).Append(" field:").Append(field).Append(" resourceList:").Append(inList).AppendLine();
            }
            var outFile = suffix == null ? game.ToString() + "_resource_fields.txt" : game.ToString() + "_resource_fields" + suffix + ".txt";
            File.WriteAllText(ProjectSettings.GlobalizePath(ReachForGodot.GetUserdataPath(outFile)), sb.ToString());
        }
    }

    public void ApplyRszPatches()
    {
        foreach (var (game, list) in dict) {
            var config = ReachForGodot.GetAssetConfig(game);
            TypeCache.MarkRSZResourceFields(list.resources
                .Where(item => !list.nonResources.Contains((item.cls, item.field)))
                .Select(item => (item.cls, item.field, item.ext)),
                config);
        }
    }

    public void CheckInstances(SupportedGame game, RSZFile rszFile, List<ResourceInfo> resourceList)
    {
        if (!dict.TryGetValue(game, out var list)) dict[game] = list = new();
        foreach (var inst in rszFile.InstanceList) {
            if (inst != null && inst.HasValues) {
                FindResourceFields(resourceList, inst, list);
            }
        }
    }

    private void FindResourceFields(List<ResourceInfo> resourceList, RszInstance instance, ResourceList resourceFields)
    {
        for (var i = 0; i < instance.RszClass.fields.Length; i++) {
            var field = instance.RszClass.fields[i];
            if (field.type is RszFieldType.String or RszFieldType.Resource) {
                if (instance.Values[i] is string value) {
                    CheckStringForResource(resourceList, instance, resourceFields, field, value);
                } else if (instance.Values[i] is List<object> list && list.FirstOrDefault() is string) {
                    foreach (var element in list.OfType<string>()) {
                        if (CheckStringForResource(resourceList, instance, resourceFields, field, element)) {
                            break;
                        }
                    }
                }
            }
        }
    }

    private static bool CheckStringForResource(List<ResourceInfo> resourceList, RszInstance instance, ResourceList resourceFields, RszField field, string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        var cls = instance.RszClass.name;
        if (resourceFields.nonResources.Contains((cls, field.name))) return false;

        var seemsLikePath = value != null && value.Contains('/') && value.Contains('.');
        if (!seemsLikePath) {
            // there are fields that can seem like paths (e.g. developer comments) but turns out aren't, this will cross those out
            resourceFields.nonResources.Add((cls, field.name));
            return false;
        }

        var isInResourceList = string.IsNullOrEmpty(value) ? (bool?)null : resourceList.Any(r => r.Path?.Equals(value, StringComparison.OrdinalIgnoreCase) == true);

        // better not store these as resources, so we don't require all folders and subfolders to always be imported
        if (cls == "via.Folder" || cls == "via.Prefab") return false;

        var ext = Path.GetExtension(value);
        if (string.IsNullOrEmpty(ext)) ext = string.Empty;
        else ext = ext.Substring(1);
        resourceFields.resources.Add((cls, field.name, ext, isInResourceList));

        return true;
    }

    public void Dispose()
    {
        DumpFindings(null);
    }
}

public static class GameObjectRefResolver
{
    public static void CheckInstances(SupportedGame game, PfbFile pfb)
    {
        foreach (var refinfo in pfb.GameObjectRefInfoList) {
            var src = pfb.RSZ.ObjectList[(int)refinfo.Data.objectId];
            var refFields = src.RszClass.fields.Where(f => f.IsGameObjectRef()).ToArray();

            var cache = TypeCache.GetClassInfo(game, src.RszClass.name);
            var propInfoDict = cache.PfbRefs;
            if (propInfoDict?.Count == refFields.Length) {
                // already resolved
                continue;
            }

            var propInfo = propInfoDict?.GetValueOrDefault(src.RszClass.name);
            var refValues = pfb.GameObjectRefInfoList.Where(rf =>
                rf.Data.objectId == src.ObjectTableIndex &&
                (rf.Data.arrayIndex == 0 || 1 == pfb.GameObjectRefInfoList.Count(rf2 => rf2.Data.objectId == src.ObjectTableIndex))
            ).OrderBy(b => b.Data.propertyId);

            if (refFields.Length == refValues.Count()) {
                propInfoDict = new();
                int i = 0;
                foreach (var propId in refValues.Select(r => r.Data.propertyId)) {
                    var refField = refFields[i++];
                    var prop = new PrefabGameObjectRefProperty() { PropertyId = propId, AutoDetected = true };
                    propInfoDict[refField.name] = prop;
                    GD.Print($"Auto-detected GameObjectRef property {src.RszClass.name} {refField.name} as propertyId {propId}.");
                }
                TypeCache.UpdatePfbGameObjectRefCache(game, src.RszClass.name, propInfoDict);
            } else {
                GD.PrintErr($"Failed to resolve GameObjectRef properties in class {src.RszClass.name}.");
            }
        }
    }
}

public static class BinaryTools
{
    public static bool CompareBinaryFiles(string file1, string file2)
    {
        using var f1 = new BinaryReader(File.OpenRead(file1));
        using var f2 = new BinaryReader(File.OpenRead(file2));

        if (f1.BaseStream.Length != f2.BaseStream.Length) return false;

        var len = f1.BaseStream.Length;
        for (int i = 0; i < len; i += 4) {
            var b1 = f1.ReadInt32();
            var b2 = f2.ReadInt32();
            if (b1 != b2) {
                GD.PrintErr($"Found file {file1} and {file2} mismatch at offset " + i.ToString());
                return false;
            }
        }

        return true;
    }

    private static string DumpStreamToFile(MemoryStream srcStream, string extension)
    {
        var outFn = Path.GetTempFileName() + extension;
        using var outfStream = File.Create(outFn);
        srcStream.WriteTo(outfStream);
        return outFn;
    }

    public static bool CompareFileOutput(string sourceFile, BaseFile file)
    {
        using var f1 = new BinaryReader(File.OpenRead(sourceFile));
        var outStream = new MemoryStream((int)file.FileHandler.Stream.Length);
        file.WriteTo(new FileHandler(outStream) { FileVersion = file.FileHandler.FileVersion });

        outStream.Flush();
        outStream.Seek(0, SeekOrigin.Begin);
        using var f2 = new BinaryReader(outStream);

        if (f1.BaseStream.Length != f2.BaseStream.Length) {
            // var ext = Path.GetExtension(Path.GetFileNameWithoutExtension(sourceFile)) + Path.GetExtension(sourceFile);
            // GD.PrintErr($"File {sourceFile} output length mismatch: {DumpStreamToFile(outStream, ext)}");
            GD.PrintErr($"File {sourceFile} output length mismatch");
            return false;
        }

        var len = f1.BaseStream.Length;
        for (int i = 0; i < len; i += 4) {
            var b1 = f1.ReadInt32();
            var b2 = f2.ReadInt32();
            if (b1 != b2) {
                var ext = Path.GetExtension(Path.GetFileNameWithoutExtension(sourceFile)) + Path.GetExtension(sourceFile);
                GD.PrintErr($"Found file {sourceFile} and output mismatch at offset {i} / {i.ToString("X")}:  {DumpStreamToFile(outStream, ext)}");
                return false;
            }
        }

        return true;
    }
}

public static class ReaGETools
{
    private static Dictionary<SupportedGame, Dictionary<string, HashSet<string>>> whitelistedDuplicates = new () {
        { SupportedGame.DevilMayCry5, new Dictionary<string, HashSet<string>>() {
            { "app.MaterialChangeController.MaterialInfo", ["Materials"] },
        } },
        { SupportedGame.ResidentEvil2, new Dictionary<string, HashSet<string>>() {
            { "app.ropeway.environment.EnvironmentBoundaryClassifier", ["MyselfMap", "TargetMap"] },
            { "app.ropeway.posteffect.param.ColorCorrect", ["LinearParamsAt"] },
        } }
    };

    public static void FindDuplicateRszObjectInstances(SupportedGame game, RSZFile file, string filepath)
    {
        var instances = new Dictionary<RszInstance, RszInstance>();
        foreach (var instance in file.InstanceList) {
            if (!instance.HasValues) {
                if (instance.RSZUserData is RSZUserDataInfo_TDB_LE_67 embed && embed.EmbeddedRSZ != null) {
                    FindDuplicateRszObjectInstances(game, embed.EmbeddedRSZ, filepath + " (Embedded RSZ)");
                }
                continue;
            }

            for (var fieldIndex = 0; fieldIndex < instance.RszClass.fields.Length; fieldIndex++) {
                var field = instance.RszClass.fields[fieldIndex];
                if (field.type == RszFieldType.Object) {
                    var value = instance.Values[fieldIndex];
                    var values = field.array ? ((List<object>)value).OfType<RszInstance>().ToArray() : [(RszInstance)value];
                    for (var i = 0; i < values.Length; i++) {
                        var val = values[i];
                        if (val.Index != 0 && !instances.TryAdd(val, instance)) {
                            var isWhitelisted = whitelistedDuplicates.GetValueOrDefault(game)?.GetValueOrDefault(instance.RszClass.name)?.Contains(field.name);
                            if (isWhitelisted != true) {
                                var valueIndex = field.array ? $"[{i}]" : "";
                                GD.PrintErr($"Found duplicate rsz instance reference - likely read error, verify correctness please.\nObject {instance} field {fieldIndex} {field.name}{valueIndex}: value {val} previously referenced from {instances[val]}.");
                                GD.PrintErr($"Filepath: {filepath}");
                                GD.PrintErr("If the reference is correct, add it to the whitelist");
                                GD.PrintErr("If the reference is not correct, add or modify the field patch list of the class in the rsz patch JSON:");
                                GD.PrintErr($"{{\n  \"Name\": \"{field.name}\",\n  \"Type\": \"S32\"\n}}");
                            }
                        }
                    }
                }
            }
        }
    }
}