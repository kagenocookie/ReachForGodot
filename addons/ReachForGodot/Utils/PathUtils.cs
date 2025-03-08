using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

namespace RGE;

public static class PathUtils
{
    private static readonly Dictionary<SupportedGame, Dictionary<string, int>> extensionVersions = new();

    public static REFileFormat GetFileFormat(ReadOnlySpan<char> filename)
    {
        var versionDot = filename.LastIndexOf('.');
        if (versionDot == -1) return REFileFormat.Unknown;

        var extDot = filename.Slice(0, versionDot).LastIndexOf('.');
        if (extDot == -1) return new REFileFormat(GetFileFormatFromExtension(filename[(versionDot + 1)..]), -1);

        if (!int.TryParse(filename.Slice(versionDot + 1), out var version)) {
            return new REFileFormat(GetFileFormatFromExtension(filename[versionDot..]), -1);
        }

        var fmt = GetFileFormatFromExtension(filename[(extDot + 1)..versionDot]);
        return new REFileFormat(fmt, version);
    }

    [return: NotNullIfNotNull(nameof(filepath))]
    public static string? NormalizeResourceFilepath(string? filepath)
    {
        if (filepath == null) return null;
        if (filepath.StartsWith("res://") == true) {
            throw new Exception("Can't normalize godot res:// filepath");
        }
        return NormalizeSourceFilePath(GetFilepathWithoutVersion(filepath));
    }

    public static string GetFilepathWithoutVersion(string filepath)
    {
        var versionDot = filepath.LastIndexOf('.');
        if (versionDot != -1 && int.TryParse(filepath.AsSpan().Slice(versionDot + 1), out _)) {
            return filepath.Substring(0, versionDot);
        }

        return filepath;
    }

    public static string GetFilenameWithoutExtensionOrVersion(string filename)
    {
        var versionDot = filename.LastIndexOf('.');
        if (versionDot == -1 && int.TryParse(filename.AsSpan()[(versionDot+1)..], out _)) return filename.GetFile().GetBaseName();

        return filename.GetFile().GetBaseName().GetBaseName();
    }

    public static RESupportedFileFormats GetFileFormatFromExtension(ReadOnlySpan<char> extension)
    {
        if (extension.SequenceEqual("mesh")) return RESupportedFileFormats.Mesh;
        if (extension.SequenceEqual("tex")) return RESupportedFileFormats.Texture;
        if (extension.SequenceEqual("scn")) return RESupportedFileFormats.Scene;
        if (extension.SequenceEqual("pfb")) return RESupportedFileFormats.Prefab;
        if (extension.SequenceEqual("user")) return RESupportedFileFormats.Userdata;
        if (extension.SequenceEqual("mdf2")) return RESupportedFileFormats.Material;
        return RESupportedFileFormats.Unknown;
    }

    public static string? GetFileExtensionFromFormat(RESupportedFileFormats format) => format switch {
        RESupportedFileFormats.Mesh => "mesh",
        RESupportedFileFormats.Texture => "tex",
        RESupportedFileFormats.Material => "mdf2",
        RESupportedFileFormats.Scene => "scn",
        RESupportedFileFormats.Prefab => "pfb",
        RESupportedFileFormats.Userdata => "user",
        _ => null,
    };

    public static Type GetResourceTypeFromFormat(RESupportedFileFormats format) => format switch {
        RESupportedFileFormats.Mesh => typeof(MeshResource),
        RESupportedFileFormats.Texture => typeof(Texture),
        RESupportedFileFormats.Material => typeof(MaterialResource),
        RESupportedFileFormats.Scene => typeof(PackedScene),
        RESupportedFileFormats.Prefab => typeof(PackedScene),
        RESupportedFileFormats.Userdata => typeof(UserdataResource),
        _ => typeof(REResource),
    };

    private static bool TryFindFileExtensionVersion(AssetConfig config, string extension, out int version)
    {
        if (!extensionVersions.TryGetValue(config.Game, out var versions)) {
            if (File.Exists(config.Paths.ExtensionVersionsCacheFilepath)) {
                using var fs = File.OpenRead(config.Paths.ExtensionVersionsCacheFilepath);
                versions = JsonSerializer.Deserialize<Dictionary<string, int>>(fs, TypeCache.jsonOptions);
            }
            extensionVersions[config.Game] = versions ??= new Dictionary<string, int>();
        }

        return versions.TryGetValue(extension, out version);
    }

    private static void UpdateFileExtension(AssetConfig config, string extension, int version)
    {
        if (!extensionVersions.TryGetValue(config.Game, out var versions)) {
            extensionVersions[config.Game] = versions = new Dictionary<string, int>();
        }
        versions[extension] = version;
        var path = config.Paths.ExtensionVersionsCacheFilepath;
        Directory.CreateDirectory(path.GetBaseDir());
        using var fs = File.Create(path);
        JsonSerializer.Serialize(fs, versions, TypeCache.jsonOptions);
    }

    public static int GuessFileVersion(string relativePath, RESupportedFileFormats format, AssetConfig config)
    {
        if (format == RESupportedFileFormats.Unknown) {
            format = GetFileFormat(relativePath).format;
        }
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

        var fullpath = RelativeToFullPath(relativePath, config);
        var ext = GetFileExtensionFromFormat(format) ?? relativePath.GetExtension();
        if (TryFindFileExtensionVersion(config, ext, out var version)) {
            return version;
        }

        var dir = fullpath.GetBaseDir();
        if (!Directory.Exists(dir)) {
            // TODO: this is where we try to retool it out of the pak files
            GD.PrintErr("Asset not found: " + fullpath);
            return -1;
        }

        var first = Directory.EnumerateFiles(dir, $"*.{ext}.*").FirstOrDefault();
        if (first != null && int.TryParse(first.GetExtension(), out version)) {
            UpdateFileExtension(config, ext, version);
            return version;
        }

        return -1;
    }

    public static string? GetLocalizedImportPath(string fullSourcePath, AssetConfig config)
    {
        var path = FullOrRelativePathToImportPath(fullSourcePath, GetFileFormat(fullSourcePath).format, config, true);
        if (string.IsNullOrEmpty(path)) return null;

        return ProjectSettings.LocalizePath(path);
    }

    public static string? GetAssetImportPath(string? fullSourcePath, RESupportedFileFormats format, AssetConfig config)
    {
        if (fullSourcePath == null) return null;
        return ProjectSettings.LocalizePath(FullOrRelativePathToImportPath(fullSourcePath, format, config, false));
    }

    public static string AppendFileVersion(string filename, AssetConfig config)
    {
        var fmt = GetFileFormat(filename);
        if (fmt.version != -1) {
            return filename;
        }

        var version = GuessFileVersion(filename, fmt.format, config);
        if (version == -1) {
            GD.PrintErr("Could not determine file version for file: " + filename);
            return filename;
        }

        return $"{filename}.{version}";
    }

    /// <summary>
    /// Search through all known file paths for the game to find the full path to a file.<br/>
    /// Search priority: Override > Chunk path > Additional paths
    /// </summary>
    /// <returns>The resolved path, or null if the file was not found.</returns>
    public static string? FindSourceFilePath(string? sourceFilePath, AssetConfig config)
    {
        if (Path.IsPathRooted(sourceFilePath)) {
            return File.Exists(sourceFilePath) ? sourceFilePath : null;
        }

        if (sourceFilePath == null) return null;
        sourceFilePath = AppendFileVersion(sourceFilePath, config);

        if (!string.IsNullOrEmpty(config.Paths.SourcePathOverride) && File.Exists(Path.Combine(config.Paths.SourcePathOverride, sourceFilePath))) {
            return Path.Combine(config.Paths.SourcePathOverride, sourceFilePath);
        }

        if (File.Exists(Path.Combine(config.Paths.ChunkPath, sourceFilePath))) {
            return Path.Combine(config.Paths.ChunkPath, sourceFilePath);
        }

        foreach (var extra in config.Paths.AdditionalPaths) {
            if (File.Exists(Path.Combine(extra, sourceFilePath))) {
                return Path.Combine(extra, sourceFilePath);
            }
        }

        return null;
    }

    /// <summary>
    /// Search through all known file paths for the game to find the full path to a file.<br/>
    /// Search priority: Override > Chunk path > Additional paths
    /// </summary>
    /// <returns>The resolved path, or null if the file was not found.</returns>
    public static IEnumerable<LabelledPathSetting> FindFileSourceFolders(string? sourceFilePath, AssetConfig config)
    {
        if (Path.IsPathRooted(sourceFilePath)) {
            sourceFilePath = FullToRelativePath(sourceFilePath, config);
        }

        if (string.IsNullOrEmpty(sourceFilePath)) yield break;

        sourceFilePath = AppendFileVersion(sourceFilePath, config);

        var overridePath = config.Paths.SourcePathOverride;

        if (File.Exists(Path.Combine(config.Paths.ChunkPath, sourceFilePath))) {
            yield return new LabelledPathSetting(Path.Combine(config.Paths.ChunkPath, sourceFilePath), "Chunks folder");
            if (overridePath == config.Paths.ChunkPath) overridePath = null;
        }

        foreach (var extra in config.Paths.AdditionalPaths) {
            if (extra != config.Paths.ChunkPath && File.Exists(Path.Combine(extra, sourceFilePath))) {
                yield return extra;
                if (overridePath == config.Paths.ChunkPath) overridePath = null;
            }
        }

        if (!string.IsNullOrEmpty(overridePath)) {
            yield return new LabelledPathSetting(overridePath, "Temp override path");
        }
    }

    public static string RelativeToFullPath(string relativeSourcePath, AssetConfig config)
    {
        if (!string.IsNullOrEmpty(config.Paths.SourcePathOverride)) {
            var overridePath = Path.Join(config.Paths.ChunkPath, relativeSourcePath);
            if (File.Exists(overridePath)) {
                return NormalizeSourceFilePath(overridePath);
            }
        }

        return NormalizeSourceFilePath(Path.Join(config.Paths.ChunkPath, relativeSourcePath));
    }

    /// <summary>
    /// Normalize a file path - replace any backslashes (\) with forward slashes (/)
    /// </summary>
    public static string NormalizeSourceFilePath(string filepath)
    {
        return filepath.Replace('\\', '/');
    }

    /// <summary>
    /// Normalize a folder path - replace any backslashes (\) with forward slashes (/), add a trailing slash.
    /// </summary>
    public static string NormalizeSourceFolderPath(string folderPath)
    {
        folderPath = folderPath.Replace('\\', '/');
        if (!folderPath.EndsWith('/')) {
            folderPath += '/';
        }
        return folderPath;
    }

    public static string? GetSourceFileBasePath(string fullSourcePath, AssetConfig config)
    {
        fullSourcePath = NormalizeSourceFilePath(fullSourcePath);

        if (!string.IsNullOrEmpty(config.Paths.SourcePathOverride) && fullSourcePath.StartsWith(config.Paths.SourcePathOverride)) {
            return config.Paths.SourcePathOverride;
        }

        if (fullSourcePath.StartsWith(config.Paths.ChunkPath)) {
            return config.Paths.ChunkPath;
        }

        foreach (var extra in config.Paths.AdditionalPaths) {
            if (fullSourcePath.StartsWith(extra)) {
                return extra;
            }
        }

        var stmroot = fullSourcePath.IndexOf("/stm/", StringComparison.OrdinalIgnoreCase);
        if (stmroot == -1) {
            stmroot = fullSourcePath.IndexOf("/x64/", StringComparison.OrdinalIgnoreCase);
        }
        if (stmroot != -1) {
            return NormalizeSourceFolderPath(fullSourcePath.Substring(0, stmroot + 5));
        }

        GD.PrintErr("Could not determine import file base path. Make sure to configure the ChunkPath setting correctly in editor settings.\nPath: " + fullSourcePath);
        return null;
    }

    public static string? FullToRelativePath(string fullSourcePath, AssetConfig config)
    {
        var basepath = GetSourceFileBasePath(fullSourcePath, config);
        return basepath == null ? null : fullSourcePath.Replace(basepath, "");
    }

    public static string? ImportPathToRelativePath(string importPath, AssetConfig config)
    {
        if (string.IsNullOrEmpty(importPath)) return null;

        var relativePath = importPath.Replace("res://" + config.AssetDirectory, "");
        if (relativePath.StartsWith('/')) relativePath = relativePath.Substring(1);
        return PathUtils.GetFilepathWithoutVersion(relativePath);
    }

    private static string? FullOrRelativePathToImportPath(string sourcePath, RESupportedFileFormats fmt, AssetConfig config, bool resource)
    {
        var relativePath = Path.IsPathRooted(sourcePath) ? FullToRelativePath(sourcePath, config) : sourcePath;
        if (relativePath == null) return null;

        relativePath = AppendFileVersion(relativePath, config);

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
}