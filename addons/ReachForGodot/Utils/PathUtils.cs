using System.Diagnostics.CodeAnalysis;
using System.Security.AccessControl;
using System.Text.Json;
using Godot;

namespace ReaGE;

public static class PathUtils
{
    private static readonly Dictionary<SupportedGame, Dictionary<string, int>> extensionVersions = new();

    private sealed record FormatDescriptor(string extension, Type resourceType, RESupportedFileFormats format);

    private static readonly List<FormatDescriptor> formats = new();
    private static readonly Dictionary<RESupportedFileFormats, FormatDescriptor> formatToDescriptor = new();
    private static readonly Dictionary<SupportedGame, HashSet<string>> ignoredFilepaths = new();

    private static readonly Dictionary<RESupportedFileFormats, Func<REResource>> resourceFactory = new();
    public static void RegisterFileFormat(RESupportedFileFormats format, string extension, Type resourceType)
    {
        var desc = new FormatDescriptor(extension, resourceType, format);

        formats.Add(desc);
        formatToDescriptor[format] = desc;
    }

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
        if (versionDot == -1 && int.TryParse(filename.AsSpan()[(versionDot + 1)..], out _)) return filename.GetFile().GetBaseName();

        return filename.GetFile().GetBaseName().GetBaseName();
    }

    public static RESupportedFileFormats GetFileFormatFromExtension(ReadOnlySpan<char> extension)
    {
        foreach (var desc in formats) {
            if (extension.SequenceEqual(desc.extension)) return desc.format;
        }
        return RESupportedFileFormats.Unknown;
    }

    public static string? GetFileExtensionFromFormat(RESupportedFileFormats format) => formatToDescriptor.GetValueOrDefault(format)?.extension;
    public static Type GetResourceTypeFromFormat(RESupportedFileFormats format) => formatToDescriptor.TryGetValue(format, out var desc) ? desc.resourceType : typeof(REResource);

    public static int GetFileFormatVersion(RESupportedFileFormats format, GamePaths config)
    {
        if (TryGetFileExtensionVersion(config, GetFileExtensionFromFormat(format)!, out var version)) {
            return version;
        }

        return 0;
    }

    public static bool TryGetFileExtensionVersion(GamePaths config, string extension, out int version)
    {
        if (!extensionVersions.TryGetValue(config.Game, out var versions)) {
            if (File.Exists(config.ExtensionVersionsCacheFilepath)) {
                using var fs = File.OpenRead(config.ExtensionVersionsCacheFilepath);
                versions = JsonSerializer.Deserialize<Dictionary<string, int>>(fs, TypeCache.jsonOptions);
            }
            extensionVersions[config.Game] = versions ??= new Dictionary<string, int>();
        }

        return versions.TryGetValue(extension, out version);
    }

    private static void UpdateFileExtension(GamePaths config, string extension, int version)
    {
        if (!extensionVersions.TryGetValue(config.Game, out var versions)) {
            extensionVersions[config.Game] = versions = new Dictionary<string, int>();
        }
        versions[extension] = version;
        var path = config.ExtensionVersionsCacheFilepath;
        Directory.CreateDirectory(path.GetBaseDir());
        using var fs = File.Create(path);
        JsonSerializer.Serialize(fs, versions, TypeCache.jsonOptions);
    }

    public static int GuessFileVersion(string relativePath, RESupportedFileFormats format, AssetConfig config)
    {
        if (format == RESupportedFileFormats.Unknown) {
            format = GetFileFormat(relativePath).format;
        }

        var ext = GetFileExtensionFromFormat(format) ?? relativePath.GetExtension();
        if (TryGetFileExtensionVersion(config.Paths, ext, out var version)) {
            return version;
        }

        if (relativePath.StartsWith('@')) {
            // IDK what these @'s are, but so far I've only found them for sounds at the root folder
            // seems to be safe to ignore, though we may need to handle them when putting the files back
            relativePath = relativePath.Substring(1);
        }
        // TODO need special handling for file extensions sbnk, spck because they're ".1.x64" or ".1.x64.en"

        // see if there's any files at all with the same file ext in whatever dir the file in question is located in
        var dir = RelativeToFullPath(relativePath, config).GetBaseDir();
        if (Directory.Exists(dir)) {
            var first = Directory.EnumerateFiles(dir, $"*.{ext}.*").FirstOrDefault();
            if (first != null && int.TryParse(first.GetExtension(), out version)) {
                UpdateFileExtension(config.Paths, ext, version);
                return version;
            }
        }

        GD.PrintErr("Could not determine file version for file: " + relativePath);
        return -1;
    }

    public static string? GetLocalizedImportPath(string fullSourcePath, AssetConfig config)
    {
        var path = FullOrRelativePathToImportPath(fullSourcePath, GetFileFormat(fullSourcePath).format, config, true);
        if (string.IsNullOrEmpty(path)) return null;

        return ProjectSettings.LocalizePath(path);
    }

    /// <summary>
    /// Gets the path that a resource's asset file will get imported to. This is the mesh/texture/audio/other linked resource, and not the main resource file.
    /// </summary>
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
            return filename;
        }

        return $"{filename}.{version}";
    }

    public static void ExtractFileVersionsFromList(SupportedGame game, string listFilepath)
    {
        if (!File.Exists(listFilepath)) {
            GD.PrintErr("List file '" + listFilepath + "' not found");
            return;
        }
        var paths = ReachForGodot.GetPaths(game) ?? new GamePaths(game) { FilelistPath = listFilepath };
        using var file = new StreamReader(File.OpenRead(listFilepath));
        while (!file.EndOfStream) {
            var line = file.ReadLine();
            if (string.IsNullOrEmpty(line)) continue;

            var versionStr = line.GetExtension();
            var ext = line.GetBaseName()?.GetExtension();
            if (!string.IsNullOrEmpty(ext) && !TryGetFileExtensionVersion(paths, ext, out _) && int.TryParse(versionStr, out var version)) {
                UpdateFileExtension(paths, ext, version);
            }
        }
    }

    public static string GetFilepathWithoutNativesFolder(string path)
    {
        path = path.Replace('\\', '/');
        if (path.EndsWith('/')) {
            path = path[..^1];
        }
        if (path.EndsWith("/natives/x64", StringComparison.OrdinalIgnoreCase)) {
            path = path.Substring(0, path.IndexOf("/natives/x64", StringComparison.OrdinalIgnoreCase));
        }
        if (path.EndsWith("/natives/stm", StringComparison.OrdinalIgnoreCase)) {
            path = path.Substring(0, path.IndexOf("/natives/stm", StringComparison.OrdinalIgnoreCase));
        }
        if (path.StartsWith("natives/x64/", StringComparison.OrdinalIgnoreCase)) {
            path = path.Substring("natives/x64/".Length);
        }
        if (path.StartsWith("natives/stm/", StringComparison.OrdinalIgnoreCase)) {
            path = path.Substring("natives/stm/".Length);
        }
        return path;
    }

    public static IEnumerable<string> GetFilesByExtensionFromListFile(string? listFilepath, string extension, string? basePath)
    {
        if (!File.Exists(listFilepath)) {
            GD.PrintErr("List file '" + listFilepath + "' not found");
            yield break;
        }

        if (basePath != null) {
            basePath = GetFilepathWithoutNativesFolder(basePath);
        }

        using var file = new StreamReader(File.OpenRead(listFilepath));
        while (!file.EndOfStream) {
            var line = file.ReadLine();
            if (string.IsNullOrEmpty(line)) continue;

            if (line.EndsWith(extension)) {
                yield return basePath == null ? line : Path.Combine(basePath, line);
            }
        }
    }

    public static bool IsIgnoredFilepath(string filepath, AssetConfig config)
    {
        if (!ignoredFilepaths.TryGetValue(config.Game, out var list)) {
            ignoredFilepaths[config.Game] = list = new();
            if (File.Exists(config.Paths.IgnoredFilesListPath)) {
                using var f = new StreamReader(File.OpenRead(config.Paths.IgnoredFilesListPath));
                while (!f.EndOfStream) {
                    var line = f.ReadLine();
                    if (!string.IsNullOrWhiteSpace(line)) list.Add(GetFilepathWithoutNativesFolder(line));
                }
            }
        }
        filepath = GetFilepathWithoutNativesFolder(AppendFileVersion(filepath, config)).ToLowerInvariant();
        return list.Contains(filepath);
    }

    public static IEnumerable<string> FindMissingFiles(string extension, AssetConfig config)
    {
        var listfile = config.Paths.FilelistPath;
        var basepath = GetFilepathWithoutNativesFolder(config.Paths.ChunkPath);
        if (!File.Exists(listfile) || string.IsNullOrEmpty(basepath)) yield break;

        extension = AppendFileVersion(extension.StartsWith('.') ? extension : "." + extension, config);

        using var f = new StreamReader(File.OpenRead(listfile));
        while (!f.EndOfStream) {
            var line = f.ReadLine();
            if (!string.IsNullOrWhiteSpace(line) && line.EndsWith(extension)) {
                if (!File.Exists(Path.Combine(basepath, line))) {
                    yield return GetFilepathWithoutNativesFolder(line);
                }
            }
        }
    }

    public static bool IsFilepath(this ReadOnlySpan<char> path) => !Path.GetExtension(path).IsEmpty;

    /// <summary>
    /// Search through all known file paths for the game to find the full path to a file.<br/>
    /// Search priority: Override > Chunk path > Additional paths
    /// If file is not found, attempts to extract from PAK files if configured.
    /// </summary>
    /// <returns>The resolved path, or null if the file was not found.</returns>
    public static string? FindSourceFilePath(string? sourceFilePath, AssetConfig config, bool autoExtract = true)
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

        if (autoExtract && !IsIgnoredFilepath(sourceFilePath, config) && FileUnpacker.TryExtractFile(sourceFilePath, config) && File.Exists(Path.Combine(config.Paths.ChunkPath, sourceFilePath))) {
            return Path.Combine(config.Paths.ChunkPath, sourceFilePath);
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
            yield return new LabelledPathSetting(config.Paths.ChunkPath, "Chunks folder");
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

    public static AssetConfig? GuessAssetConfigFromImportPath(string importPath)
    {
        foreach (var cfg in ReachForGodot.AssetConfigs) {
            if (importPath.StartsWith("res://" + cfg.AssetDirectory)) {
                return cfg;
            }
        }

        return null;
    }

    private static string? FullOrRelativePathToImportPath(string sourcePath, RESupportedFileFormats fmt, AssetConfig config, bool resource)
    {
        var relativePath = Path.IsPathRooted(sourcePath) ? FullToRelativePath(sourcePath, config) : sourcePath;
        if (relativePath == null) return null;

        relativePath = GetFilepathWithoutVersion(relativePath);

        var targetPath = Path.Combine(config.AssetDirectory, relativePath);

        switch (fmt) {
            case RESupportedFileFormats.Mesh:
                return targetPath + (resource ? ".tres" : ".glb");
            case RESupportedFileFormats.Texture:
                return targetPath + (resource ? ".tres" : ".dds");
            case RESupportedFileFormats.Rcol:
                return targetPath + (resource ? ".tres" : ".tscn");
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