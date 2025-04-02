namespace ReaGE.DevTools;

using System;
using System.Security.Principal;
using System.Text;
using Godot;
using RszTool;
using Shouldly;

public sealed class ResourceFieldFinder : IDisposable
{
    private Dictionary<SupportedGame, HashSet<(string cls, string field, string ext, bool)>> dict = new();

    public void DumpFindings(string? suffix)
    {
        foreach (var (game, list) in dict) {
            var sb = new StringBuilder();
            foreach (var (cls, field, ext, inList) in list.OrderBy(a => new { a.cls, a.field })) {
                sb.Append("ext:").Append(ext).Append(" class:").Append(cls).Append(" field:").Append(field).Append(" resourceList:").Append(inList).AppendLine();
            }
            var outFile = suffix == null ? game.ToString() + "_resource_fields.txt" : game.ToString() + "_resource_fields" + suffix + ".txt";
            File.WriteAllText(Path.Combine(ReachForGodot.UserdataPath, outFile), sb.ToString());
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

    private void FindResourceFields(List<ResourceInfo> resourceList, RszInstance instance, HashSet<(string, string, string, bool)> resourceFields)
    {
        for (var i = 0; i < instance.RszClass.fields.Length; i++) {
            var f = instance.RszClass.fields[i];
            if (f.type is RszFieldType.String or RszFieldType.Resource) {
                var value = instance.Values[i] as string;

                var isInResourceList = resourceList.Any(r => r.Path?.Equals(value, StringComparison.OrdinalIgnoreCase) == true);
                if (!isInResourceList && f.type == RszFieldType.String) {
                    // do extra checks
                    if (value == null || !value.Contains('/') || !value.Contains('.')) {
                        continue;
                    }
                }

                var cls = instance.RszClass.name;
                var ext = Path.GetExtension(value);
                if (string.IsNullOrEmpty(ext)) ext = string.Empty;
                else ext = ext.Substring(1);
                resourceFields.Add((cls, $"[{i}]{f.name}", ext, isInResourceList));
            }
        }
    }

    public void Dispose()
    {
        DumpFindings(null);
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