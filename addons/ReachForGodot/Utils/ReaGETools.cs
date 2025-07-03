namespace ReaGE.Tools;

using System;
using Godot;
using ReeLib;
using ReeLib.Efx;

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
    private static readonly byte[] RSZBytes = [0x52, 0x53, 0x5a, 0];

    public struct InstanceInfoStruct {
        public uint typeId;
        public uint CRC;

        public InstanceInfo GetInstanceInfo() => new InstanceInfo(GameVersion.unknown) { typeId = typeId, CRC = CRC };
    }

    public static IEnumerable<InstanceInfoStruct>? GetRSZInstanceInfos(string filepath)
    {
        // read directly from file for finding the RSZ data - so we don't necesarily fully load every single fully into memory
        using var fs = File.OpenRead(filepath);
        var handler = new FileHandler(fs);
        var rszOffset = handler.FindBytes(RSZBytes, new ReeLib.Common.SearchParam() { end = -1 });
        if (rszOffset == -1) {
            yield break;
        }

        var header = new RSZFile.RszHeader();
        handler.Seek(rszOffset);
        header.Read(handler);
        handler.Seek(rszOffset + header.instanceOffset);
        for (int i = 0; i < header.instanceCount; i++)
        {
            InstanceInfoStruct instanceInfo = new();
            handler.Read(ref instanceInfo);
            yield return instanceInfo;
        }
    }

    public static bool FileCanContainRSZData(string filepath)
    {
        var ext = PathUtils.GetFilenameExtensionWithoutSuffixes(filepath);
        var format = PathUtils.GetFileFormatFromExtension(ext);
        return format is KnownFileFormats.Scene
            or KnownFileFormats.Prefab
            or KnownFileFormats.RequestSetCollider
            or KnownFileFormats.UserData
            || ext.StartsWith("ai") // all ai* files (AIMP) have an RSZ section, though it's empty for the most part
            || ext.StartsWith("w") // many wwise related audio files contain a single main rsz instance similar to user files
            || ext == "swms" // one more audio related file
            ;
    }

    public static IEnumerable<string> FindInstancesInAnyRSZFile(string classname, SupportedGame[] games, CancellationToken? token = null)
    {
        var conv = new AssetConverter(GodotImportOptions.placeholderImport);
        foreach (var config in ReachForGodot.AssetConfigs) {
            if (!config.IsValid || config.Workspace.ListFile == null) continue;
            if (games.Length != 0 && games.Contains(config.Game)) {
                continue;
            }
            conv.Game = config.Game;
            var rszClass = conv.FileOption.RszParser.GetRSZClass(classname);
            if (rszClass == null) continue;

            GD.Print("Searching within game " + config.Game + " ...");

            var crc = rszClass.crc;
            var list = config.Workspace.ListFile;
            var basepath = ReaGE.PathUtils.GetFilepathWithoutNativesFolder(config.Paths.ChunkPath);

            foreach (var file in list.Files) {
                if (token?.IsCancellationRequested == true) yield break;
                var filepath = Path.Combine(basepath, file);
                if (!FileCanContainRSZData(filepath)) continue;

                if (!File.Exists(filepath)) {
                    if (!FileUnpacker.TryExtractFile(filepath, config) || !File.Exists(filepath)) {
                        GD.PrintErr(config.Game + " file not found: " + file);
                        continue;
                    }
                }

                var match = GetRSZInstanceInfos(filepath)?.FirstOrDefault(info => info.CRC == crc);
                if (match.HasValue && match.Value.CRC == crc) {
                    yield return filepath;
                }
            }
        }
    }

    public static IEnumerable<string> FindEfxByAttribute(EfxAttributeType efxType, EfxVersion version, CancellationToken? token = null)
    {
        var conv = new AssetConverter(GodotImportOptions.placeholderImport);
        foreach (var config in ReachForGodot.AssetConfigs) {
            if (!config.IsValid || config.Workspace.ListFile == null) continue;
            var efxVer = config.Game.GameToEfxVersion();
            if (version != EfxVersion.Unknown && efxVer != version) {
                continue;
            }
            if (efxVer == EfxVersion.Unknown) continue;

            conv.Game = config.Game;
            var attrType = EfxAttributeTypeRemapper.GetAttributeInstanceType(efxType, efxVer);
            if (attrType == null) continue;

            GD.Print("Searching within game " + config.Game + " ...");

            foreach (var (filepath, str) in config.Workspace.GetFilesWithExtension("efx")) {
                if (token?.IsCancellationRequested == true) yield break;

                var file = conv.Efx.CreateFile(filepath);

                try {
                    file.Read();
                } catch (Exception e) {
                    GD.PrintErr($"Failed to read file {file}: {e.Message}");
                    continue;
                }
                if (file.Actions.Any(a => a.Attributes.Any(attr => attr.type == efxType))) {
                    yield return filepath;
                } else if (file.Entries.Any(e => e.Attributes.Any(attr => attr.type == efxType))) {
                    yield return filepath;
                }
            }
        }
    }
}