using System.Diagnostics.CodeAnalysis;
using System.IO.IsolatedStorage;
using System.Security.AccessControl;
using System.Text.Json;
using System.Text.RegularExpressions;
using Godot;

namespace ReaGE;

public static partial class PathUtils
{
    private static readonly Dictionary<SupportedGame, FileExtensionCache> extensionInfo = new();

    private sealed record FormatDescriptor(string extension, Type resourceType, SupportedFileFormats format);

    private static readonly List<FormatDescriptor> formats = new();
    private static readonly Dictionary<SupportedFileFormats, FormatDescriptor> formatToDescriptor = new();
    private static readonly Dictionary<SupportedGame, HashSet<string>> ignoredFilepaths = new();

    private static readonly Dictionary<SupportedFileFormats, Func<REResource>> resourceFactory = new();
    public static void RegisterFileFormat(SupportedFileFormats format, string extension, Type resourceType)
    {
        var desc = new FormatDescriptor(extension, resourceType, format);

        formats.Add(desc);
        formatToDescriptor[format] = desc;
    }

    public static REFileFormat GetFileFormat(ReadOnlySpan<char> filename)
    {
        filename = GetFilenameExtensionWithSuffixes(filename);
        if (filename.IsEmpty) return REFileFormat.Unknown;
        // filename: ".mesh" OR ".mesh.123" OR ".sbnk.1.x64" OR ".sbnk.1.x64.en"

        var versionDot = filename.IndexOf('.');
        if (versionDot == -1) return new REFileFormat(GetFileFormatFromExtension(filename), -1);

        var versionEnd = filename[(versionDot + 1)..].IndexOf('.');
        if (versionEnd == -1) versionEnd = filename.Length;
        else versionEnd += versionDot + 1;

        if (!int.TryParse(filename[(versionDot + 1)..versionEnd], out var version)) {
            return new REFileFormat(GetFileFormatFromExtension(filename), -1);
        }

        var fmt = GetFileFormatFromExtension(filename[..versionDot]);
        return new REFileFormat(fmt, version);
    }

    [return: NotNullIfNotNull(nameof(filepath))]
    public static string? NormalizeResourceFilepath(string? filepath)
    {
        if (filepath == null) return null;
        if (filepath.StartsWith("res://") == true) {
            throw new Exception("Can't normalize godot res:// filepath");
        }
        return NormalizeFilePath(GetFilepathWithoutVersion(filepath));
    }

    public static string GetFilepathWithoutVersion(string filepath)
    {
        var versionDot = filepath.LastIndexOf('.');
        if (versionDot != -1 && int.TryParse(filepath.AsSpan().Slice(versionDot + 1), out _)) {
            return filepath.Substring(0, versionDot);
        }

        return filepath;
    }

    private static int GetFilenameExtensionStartIndex(ReadOnlySpan<char> filename)
    {
        var dot = filename.LastIndexOf('.');
        if (dot == -1) return filename.Contains('/') ? -1 : 0;
        var end = filename.Length;

        if (filename[(dot + 1)..].SequenceEqual("x64")) {
            end = dot;
            dot = filename[0..^4].LastIndexOf('.');
        } else if (filename.Length > 10) {
            // handle e.g. "filename.1.x64.ptbr"
            var locIndex = filename[^10..].IndexOf(".x64.");
            if (locIndex != -1) {
                locIndex = end - 10 + locIndex;
                end = locIndex;
                dot = filename[0..locIndex].LastIndexOf('.');
            }
        }
        if (dot != -1 && int.TryParse(filename[(dot + 1)..end], out _)) {
            dot = filename[..dot].LastIndexOf('.');
        }

        return dot;
    }

    public static ReadOnlySpan<char> GetFilenameExtensionWithSuffixes(ReadOnlySpan<char> filename)
    {
        var extIndex = GetFilenameExtensionStartIndex(filename);
        if (extIndex == -1) return filename;
        return filename[extIndex] == '.' ? filename[(extIndex + 1)..] : filename[extIndex..];
    }

    public static ReadOnlySpan<char> GetFilenameWithoutExtensionOrVersion(ReadOnlySpan<char> filename)
    {
        var extIndex = GetFilenameExtensionStartIndex(filename);
        if (extIndex == -1) return filename;
        return filename[extIndex] == '.' ? filename[..extIndex] : filename[..(extIndex + 1)];
    }

    public static SupportedFileFormats GetFileFormatFromExtension(ReadOnlySpan<char> extension)
    {
        foreach (var desc in formats) {
            if (extension.SequenceEqual(desc.extension)) return desc.format;
        }
        return SupportedFileFormats.Unknown;
    }

    public static string? GetFileExtensionFromFormat(SupportedFileFormats format) => formatToDescriptor.GetValueOrDefault(format)?.extension;
    public static Type GetResourceTypeFromFormat(SupportedFileFormats format) => formatToDescriptor.TryGetValue(format, out var desc) ? desc.resourceType : typeof(REResource);

    public static int GetFileFormatVersion(SupportedFileFormats format, GamePaths config)
    {
        if (TryGetFileExtensionVersion(config, GetFileExtensionFromFormat(format)!, out var version)) {
            return version;
        }

        return 0;
    }

    private sealed class FileExtensionCache
    {
        public FileExtensionCache() { }

        public Dictionary<string, int> Versions { get; set; } = null!;
        public Dictionary<string, FileExtensionInfo> Info { get; set; } = null!;

        public FileExtensionCache(Dictionary<string, int> versions)
        {
            Versions = versions;
            Info = versions.ToDictionary(k => k.Key, v => new FileExtensionInfo() { Version = v.Value });
        }

        public FileExtensionCache(Dictionary<string, FileExtensionInfo> info)
        {
            Info = info;
            Versions = info.ToDictionary(k => k.Key, v => v.Value.Version);
        }
    }

    private sealed class FileExtensionInfo
    {
        public List<string> Locales { get; set; } = new();
        public int Version { get; set; }
        public bool CanHaveX64 { get; set; }
        public bool CanNotHaveX64 { get; set; }
        public bool CanHaveLang { get; set; }
        public bool CanNotHaveLang { get; set; }
    }

    private static FileExtensionCache GetExtensionInfo(GamePaths config)
    {
        if (!extensionInfo.TryGetValue(config.Game, out var info)) {
            if (File.Exists(config.ExtensionVersionsCacheFilepath)) {
                using var fs = File.OpenRead(config.ExtensionVersionsCacheFilepath);
                info = JsonSerializer.Deserialize<FileExtensionCache>(fs, TypeCache.jsonOptions) ?? new();
            }
            extensionInfo[config.Game] = info ??= new FileExtensionCache();
        }
        return info;
    }

    private static Dictionary<string, int> GetVersionDict(GamePaths config)
    {
        return GetExtensionInfo(config).Versions;
    }

    public static bool TryGetFileExtensionVersion(GamePaths config, string extension, out int version)
    {
        return GetVersionDict(config).TryGetValue(extension, out version);
    }

    public static IEnumerable<string> GetGameFileExtensions(SupportedGame game)
    {
        return GetVersionDict(ReachForGodot.GetPaths(game)!).Keys;
    }

    private static void UpdateFileExtension(GamePaths config, string extension, int version)
    {
        var info = GetExtensionInfo(config);
        info.Versions[extension] = version;
        SerializeExtensionInfo(config, info);
    }

    private static void SerializeExtensionInfo(GamePaths config, FileExtensionCache info)
    {
        var path = config.ExtensionVersionsCacheFilepath;
        Directory.CreateDirectory(path.GetBaseDir());
        using var fs = File.Create(path);
        JsonSerializer.Serialize(fs, info, TypeCache.jsonOptions);
    }

    public static int GuessFileVersion(string relativePath, SupportedFileFormats format, AssetConfig config)
    {
        if (format == SupportedFileFormats.Unknown) {
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
    public static string? GetAssetImportPath(string? fullSourcePath, SupportedFileFormats format, AssetConfig config)
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

    [GeneratedRegex("\\.x64\\.([a-z]{2,})$")]
    private static partial Regex IsLocalizedFileRegex();

    public static void ExtractFileVersionsFromList(SupportedGame game, string listFilepath)
    {
        if (!File.Exists(listFilepath)) {
            GD.PrintErr("List file '" + listFilepath + "' not found");
            return;
        }
        var paths = ReachForGodot.GetPaths(game) ?? new GamePaths(game) { FilelistPath = listFilepath };
        using var file = new StreamReader(File.OpenRead(listFilepath));
        var extensions = GetExtensionInfo(paths);
        while (!file.EndOfStream) {
            var line = file.ReadLine();
            if (string.IsNullOrEmpty(line)) continue;

            var isLocalized = false;
            string? locale = null;
            var hasX64 = false;
            if (IsLocalizedFileRegex().IsMatch(line)) {
                hasX64 = true;
                isLocalized = true;
                locale = IsLocalizedFileRegex().Match(line).Groups[1].Value;
                line = line.GetBaseName().GetBaseName();
            } else if (line.EndsWith("x64")) {
                hasX64 = true;
                line = line.GetBaseName();
            }

            var versionStr = line.GetExtension();
            var ext = line.GetBaseName()?.GetExtension();
            if (!string.IsNullOrEmpty(ext)) {
                if (!extensions.Info.TryGetValue(ext, out var info)) {
                    extensions.Info[ext] = info = new FileExtensionInfo();
                }
                info.CanHaveX64 = hasX64 || info.CanHaveX64;
                info.CanNotHaveX64 = !hasX64 || info.CanNotHaveX64;
                info.CanHaveLang = isLocalized || info.CanHaveLang;
                info.CanNotHaveLang = !isLocalized || info.CanNotHaveLang;
                if (locale != null && !info.Locales.Contains(locale)) {
                    info.Locales.Add(locale);
                }
                if (int.TryParse(versionStr, out var version)) {
                    if (info.Version != 0 && info.Version != version) {
                        GD.PrintErr($"Warning: updating .{ext} file version from {info.Version} to {version}");
                    }
                    info.Version = version;
                    extensions.Versions[ext] = version;
                }
            }
        }
        SerializeExtensionInfo(paths, extensions);
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

    public static string GetFilepathWithNativesFolder(string path, SupportedGame game)
    {
        path = path.Replace('\\', '/');
        if (path.StartsWith("natives/")) return path;

        if (game is SupportedGame.Unknown or SupportedGame.DevilMayCry5 or SupportedGame.ResidentEvil2 or SupportedGame.ResidentEvil7) {
            return "natives/x64/" + path;
        }

        return "natives/stm/" + path;
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
    /// Get a list of possible resolved filepaths for a filename - should handle any .x64 or.x64.en suffixes
    /// </summary>
    public static IEnumerable<string> GetCandidateFilepaths(string filepath, AssetConfig config)
    {
        var basepath = GetFilenameWithoutExtensionOrVersion(filepath);
        var extensionSpan = filepath[(basepath.Length + 1)..];
        var extIndex = extensionSpan.IndexOf('.');
        FileExtensionInfo? extInfo = null;
        string? ext = null;
        if (extIndex != -1) {
            ext = extensionSpan[..extIndex];
            extInfo = GetExtensionInfo(config.Paths).Info.GetValueOrDefault(ext);
        }

        var path = GetFilepathWithNativesFolder(basepath.ToString(), config.Game);
        if (extInfo == null || ext == null) {
            yield return AppendFileVersion(path, config);
            yield break;
        }

        if (extInfo.CanNotHaveX64) {
            yield return AppendFileVersion($"{path}.{ext}", config);
        }

        if (extInfo.CanNotHaveLang && extInfo.CanHaveX64) {
            yield return AppendFileVersion($"{path}.{ext}", config) + ".x64";
        }

        if (extInfo.CanHaveLang) {
            foreach (var locale in extInfo.Locales) {
                yield return AppendFileVersion($"{path}.{ext}", config) + $".x64.{locale}";
            }
        }
    }

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

        if (sourceFilePath.StartsWith('@')) {
            // what are these?
            sourceFilePath = sourceFilePath.Substring(1);
        }

        foreach (var candidate in GetCandidateFilepaths(sourceFilePath, config)) {
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
        }

        if (autoExtract && !IsIgnoredFilepath(sourceFilePath, config) && FileUnpacker.TryExtractFile(sourceFilePath, config) && File.Exists(Path.Combine(config.Paths.ChunkPath, sourceFilePath))) {
            return Path.Combine(config.Paths.ChunkPath, sourceFilePath);
        }

        return null;
    }

    public static string? ResolveExportPath(string? basePath, string? assetPath, SupportedGame game)
    {
        if (!Path.IsPathRooted(assetPath)) {
            if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(basePath)) {
                return null;
            }

            assetPath = Path.Combine(basePath, assetPath);
        }

        var config = ReachForGodot.GetAssetConfig(game) ?? throw new Exception("Missing config for game " + game);
        return AppendFileVersion(assetPath, config);
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
                return NormalizeFilePath(overridePath);
            }
        }

        return NormalizeFilePath(Path.Join(config.Paths.ChunkPath, relativeSourcePath));
    }

    /// <summary>
    /// Normalize a file path - replace any backslashes (\) with forward slashes (/)
    /// </summary>
    public static string NormalizeFilePath(string filepath)
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
        fullSourcePath = NormalizeFilePath(fullSourcePath);

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

    private static string? FullOrRelativePathToImportPath(string sourcePath, SupportedFileFormats fmt, AssetConfig config, bool resource)
    {
        var relativePath = Path.IsPathRooted(sourcePath) ? FullToRelativePath(sourcePath, config) : sourcePath;
        if (relativePath == null) return null;

        relativePath = GetFilepathWithoutVersion(relativePath);

        var targetPath = Path.Combine(config.AssetDirectory, relativePath);

        switch (fmt) {
            case SupportedFileFormats.Mesh:
                return targetPath + (resource ? ".tres" : ".glb");
            case SupportedFileFormats.Texture:
                return targetPath + (resource ? ".tres" : ".dds");
            case SupportedFileFormats.Rcol:
                return targetPath + (resource ? ".tres" : ".tscn");
            case SupportedFileFormats.Scene:
            case SupportedFileFormats.Prefab:
                return targetPath + ".tscn";
            case SupportedFileFormats.Userdata:
                return targetPath + ".tres";
            default:
                return targetPath + ".tres";
        }
    }
}