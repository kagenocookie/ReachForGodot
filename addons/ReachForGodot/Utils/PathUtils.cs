using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Tasks;
using Godot;

namespace RGE;

public static class PathUtils
{
    public static REFileFormat GetFileFormat(string filename)
    {
        var versionDot = filename.LastIndexOf('.');
        if (versionDot == -1) return REFileFormat.Unknown;

        var extDot = filename.LastIndexOf('.', versionDot - 1);
        if (extDot == -1) return new REFileFormat(GetFileFormatFromExtension(filename.AsSpan()[(versionDot + 1)..]), -1);

        if (!int.TryParse(filename.AsSpan().Slice(versionDot + 1), out var version)) {
            return new REFileFormat(GetFileFormatFromExtension(filename.AsSpan()[versionDot..]), -1);
        }

        var fmt = GetFileFormatFromExtension(filename.AsSpan()[(extDot + 1)..versionDot]);
        return new REFileFormat(fmt, version);
    }

    [return: NotNullIfNotNull(nameof(filepath))]
    public static string? NormalizeResourceFilepath(string? filepath)
    {
        if (filepath == null) return null;
        if (filepath.StartsWith("res://") == true) {
            throw new Exception("Can't normalize godot res:// filepath");
        }
        return GetFilepathWithoutVersion(filepath).Replace('\\', '/');
    }

    public static string GetFilepathWithoutVersion(string filepath)
    {
        var versionDot = filepath.LastIndexOf('.');
        if (versionDot != -1 && int.TryParse(filepath.AsSpan().Slice(versionDot + 1), out _)) {
            return filepath.Substring(0, versionDot);
        }

        return filepath;
    }

    public static RESupportedFileFormats GetFileFormatFromExtension(ReadOnlySpan<char> extension)
    {
        if (extension.SequenceEqual("mesh")) return RESupportedFileFormats.Mesh;
        if (extension.SequenceEqual("tex")) return RESupportedFileFormats.Texture;
        if (extension.SequenceEqual("scn")) return RESupportedFileFormats.Scene;
        if (extension.SequenceEqual("pfb")) return RESupportedFileFormats.Prefab;
        if (extension.SequenceEqual("user")) return RESupportedFileFormats.Userdata;
        return RESupportedFileFormats.Unknown;
    }

    public static string? GetFileExtensionFromFormat(RESupportedFileFormats format) => format switch {
        RESupportedFileFormats.Mesh => "mesh",
        RESupportedFileFormats.Texture => "tex",
        RESupportedFileFormats.Scene => "scn",
        RESupportedFileFormats.Prefab => "pfb",
        RESupportedFileFormats.Userdata => "user",
        _ => null,
    };

    public static int GuessFileVersion(string relativePath, RESupportedFileFormats format, AssetConfig config)
    {
        switch (format) {
            case RESupportedFileFormats.Scene:
                return config.Game switch {
                    SupportedGame.ResidentEvil7 => 18,
                    SupportedGame.ResidentEvil2 => 19,
                    SupportedGame.DevilMayCry5 => 19,
                    SupportedGame.MonsterHunterWilds => 21,
                    _ => 20,
                };
            case RESupportedFileFormats.Prefab:
                return config.Game switch {
                    SupportedGame.ResidentEvil7 => 16,
                    SupportedGame.ResidentEvil2 => 16,
                    SupportedGame.DevilMayCry5 => 16,
                    SupportedGame.MonsterHunterWilds => 18,
                    _ => 17,
                };
        }

        if (relativePath.StartsWith('@')) {
            // IDK what these @'s are, but so far I've only found them for sounds at the root folder
            // seems to be safe to ignore, though we may need to handle them when putting the files back
            relativePath = relativePath.Substring(1);
        }

        var fullpath = Path.Join(config.Paths.ChunkPath, relativePath);
        var ext = GetFileExtensionFromFormat(format) ?? relativePath.GetExtension();
        var dir = fullpath.GetBaseDir();
        if (!Directory.Exists(dir)) {
            // TODO: this is where we try to retool it out of the pak files
            GD.PrintErr("Asset not found: " + fullpath);
            return -1;
        }

        var first = Directory.EnumerateFiles(dir, $"*.{ext}.*").FirstOrDefault();
        if (first != null) {
            // TODO: cache autodetected file format versions?
            return int.TryParse(first.GetExtension(), out var ver) ? ver : -1;
        }
        return -1;
    }

    public static string? GetLocalizedImportPath(string fullSourcePath, AssetConfig config)
    {
        var path = GetDefaultImportPath(fullSourcePath, GetFileFormat(fullSourcePath).format, config, true);
        if (string.IsNullOrEmpty(path)) return null;

        return ProjectSettings.LocalizePath(path);
    }

    public static string? GetAssetImportPath(string? fullSourcePath, RESupportedFileFormats format, AssetConfig config)
    {
        if (fullSourcePath == null) return null;
        return ProjectSettings.LocalizePath(GetDefaultImportPath(fullSourcePath, format, config, false));
    }

    private static string? GetDefaultImportPath(string fullSourcePath, RESupportedFileFormats fmt, AssetConfig config, bool resource)
    {
        var basepath = ReachForGodot.GetChunkPath(config.Game);
        if (basepath == null) {
            throw new ArgumentException($"{config.Game} chunk path not configured");
        }
        var relativePath = fullSourcePath.Replace(basepath, "");
        var realOsFilepath = ResolveSourceFilePath(relativePath, config);
        if (string.IsNullOrEmpty(realOsFilepath)) {
            GD.PrintErr($"{config.Game} file not found: " + relativePath);
            return null;
        }

        relativePath = realOsFilepath.Replace(basepath, "");
        var targetPath = Path.Combine(config.AssetDirectory, relativePath);

        switch (fmt) {
            case RESupportedFileFormats.Mesh:
                return targetPath + (resource ? ".tres" : ".blend");
            case RESupportedFileFormats.Texture:
                return targetPath + (resource ? ".tres" : ".dds");
            case RESupportedFileFormats.Scene:
            case RESupportedFileFormats.Prefab:
                return targetPath + ".tscn";
            case RESupportedFileFormats.Userdata:
                return targetPath + ".tres";
            default:
                return targetPath + ".tres";
        }
    }

    public static string? ResolveSourceFilePath(string? relativeSourcePath, AssetConfig config)
    {
        if (relativeSourcePath == null) return null;
        var fmt = GetFileFormat(relativeSourcePath);
        string extractedFilePath;
        if (fmt.version == -1) {
            fmt.version = GuessFileVersion(relativeSourcePath, fmt.format, config);
            if (fmt.version == -1) {
                return null;
            }
            extractedFilePath = Path.Join(config.Paths.ChunkPath, relativeSourcePath + "." + fmt.version).Replace('\\', '/');
        } else {
            extractedFilePath = Path.Join(config.Paths.ChunkPath, relativeSourcePath).Replace('\\', '/');
        }

        return extractedFilePath;
    }
}