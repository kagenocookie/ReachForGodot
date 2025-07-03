using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Godot;
using ReeLib;
using ReeLib.Common;

namespace ReaGE;

public static partial class PathUtils
{
    private static readonly Dictionary<SupportedGame, FileExtensionCache> extensionInfo = new();

    private sealed record FormatDescriptor(string extension, Type resourceType, KnownFileFormats format);

    private static readonly List<FormatDescriptor> formats = new();
    private static readonly Dictionary<KnownFileFormats, FormatDescriptor> formatToDescriptor = new();
    private static readonly Dictionary<int, FormatDescriptor> extensionToDescriptor = new();
    private static readonly Dictionary<SupportedGame, HashSet<string>> ignoredFilepaths = new();

    private static readonly Dictionary<KnownFileFormats, Func<REResource>> resourceFactory = new();
    public static void RegisterFileFormat(KnownFileFormats format, string extension, Type resourceType)
    {
        var desc = new FormatDescriptor(extension, resourceType, format);

        formats.Add(desc);
        formatToDescriptor[format] = desc;
        extensionToDescriptor.Add(GetSpanHash(extension), desc);
    }

    private static int GetSpanHash(ReadOnlySpan<char> span)
    {
        return CultureInfo.InvariantCulture.CompareInfo.GetHashCode(span, CompareOptions.Ordinal);
    }

    public static REFileFormat ParseFileFormat(ReadOnlySpan<char> filename)
    {
        return ReeLib.PathUtils.ParseFileFormat(filename);
    }

    [return: NotNullIfNotNull(nameof(filepath))]
    public static string? NormalizeResourceFilepath(string? filepath)
    {
        if (filepath == null) return null;
        if (filepath.StartsWith("res://") == true) {
            return filepath;
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

    public static bool IsSameExtension(string? path1, string? path2)
    {
        if (path1 == null || path2 == null) return false;

        var p1 = GetFilenameExtensionWithoutSuffixes(path1);
        var p2 = GetFilenameExtensionWithoutSuffixes(path2);
        return p1.SequenceEqual(p2);
    }

    private static int GetFilenameExtensionStartIndex(ReadOnlySpan<char> filename)
    {
        var dot = filename.LastIndexOf('.');
        if (dot == -1) return filename.IsEmpty || filename.Contains('/') ? -1 : 0;
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

    public static ReadOnlySpan<char> GetFilenameExtensionWithoutSuffixes(ReadOnlySpan<char> filename)
    {
        var fullExt = GetFilenameExtensionWithSuffixes(filename);
        var dot = fullExt.IndexOf('.');
        return dot == -1 ? fullExt : fullExt[0..dot];
    }

    public static ReadOnlySpan<char> GetFilepathWithoutExtensionOrVersion(ReadOnlySpan<char> filename)
    {
        var extIndex = GetFilenameExtensionStartIndex(filename);
        if (extIndex == -1) return filename;
        return filename[extIndex] == '.' ? filename[..extIndex] : filename[..(extIndex + 1)];
    }

    public static KnownFileFormats GetFileFormatFromExtension(ReadOnlySpan<char> extension)
    {
        return FileFormatExtensions.ExtensionHashToEnum(MurMur3HashUtils.GetHash(extension));
    }

    public static string? GetFileExtensionFromFormat(KnownFileFormats format) => formatToDescriptor.GetValueOrDefault(format)?.extension;
    public static Type GetResourceTypeFromFormat(KnownFileFormats format) => formatToDescriptor.TryGetValue(format, out var desc) ? desc.resourceType : typeof(REResource);

    private static string[]? _fileVersions;
    public static string[] GetKnownImportableFileVersions()
    {
        if (_fileVersions != null) return _fileVersions;
        _fileVersions = ReachForGodot.ConfiguredGames
            .Select(game => ReachForGodot.GetAssetConfig(game))
            .SelectMany(conf => conf.Workspace.GameFileFormats
                .Where(fmt => fmt.format != KnownFileFormats.Unknown)
                .Select(v => v.version.ToString()))
            .ToHashSet()
            .ToArray();

        return _fileVersions;
    }
    public static int GetFileFormatVersion(KnownFileFormats format, AssetConfig config)
    {
        var ext = GetFileExtensionFromFormat(format);
        if (ext == null) return 0;
        if (config.Workspace.TryGetFileExtensionVersion(ext, out var version)) {
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
        public bool CanHaveStm { get; set; }
        public bool CanNotHaveX64 { get; set; }
        public bool CanHaveLang { get; set; }
        public bool CanNotHaveLang { get; set; }
    }

    public static string? GetLocalizedImportPath(string sourcePath, AssetConfig config)
    {
        var path = FullOrRelativePathToImportPath(sourcePath, ParseFileFormat(sourcePath).format, config, true);
        return string.IsNullOrEmpty(path) ? null : path;
    }

    /// <summary>
    /// Gets the path that a resource's asset file will get imported to. This is the mesh/texture/audio/other linked resource, and not the main resource file.
    /// </summary>
    public static string? GetAssetImportPath(string? sourcePath, AssetConfig config)
    {
        var format = ParseFileFormat(sourcePath).format;
        return GetAssetImportPath(sourcePath, format, config);
    }
    /// <summary>
    /// Gets the path that a resource's asset file will get imported to. This is the mesh/texture/audio/other linked resource, and not the main resource file.
    /// </summary>
    public static string? GetAssetImportPath(string? sourcePath, KnownFileFormats format, AssetConfig config)
    {
        if (sourcePath == null) return null;
        return FullOrRelativePathToImportPath(sourcePath, format, config, false);
    }

    public static string AppendFileVersion(string filename, AssetConfig config)
    {
        var fmt = ParseFileFormat(filename);
        if (fmt.version != -1) {
            return filename;
        }

        var version = config.Workspace.GetFileVersion(filename);
        if (version == -1) {
            return filename;
        }

        return $"{filename}.{version}";
    }

    [GeneratedRegex("\\.x64\\.([a-z]{2,})$")]
    private static partial Regex IsLocalizedFileRegex();

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

    public static string GetFilepathWithNativesFolderPrefix(string path, SupportedGame game)
    {
        path = path.Replace('\\', '/');
        if (path.StartsWith("natives/")) return path;

        return (IsNativesX64(game) ? "natives/x64/" : "natives/stm/") + path;
    }

    public static string GetFilepathWithNativesFolderSuffix(string path, SupportedGame game)
    {
        if (string.IsNullOrEmpty(path)) return path;
        path = NormalizeSourceFolderPath(path);
        if (path.EndsWith("/natives/x64/", StringComparison.OrdinalIgnoreCase) || path.EndsWith("/natives/stm/", StringComparison.OrdinalIgnoreCase)) {
            return path;
        }

        return path + (IsNativesX64(game) ? "natives/x64/" : "natives/stm/");
    }

    private static bool IsNativesX64(SupportedGame game) => game is SupportedGame.DevilMayCry5 or SupportedGame.ResidentEvil2 or SupportedGame.ResidentEvil7;

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

    /// <summary>
    /// Get a list of possible resolved filepaths for a filename - should handle any .x64 or.x64.en suffixes
    /// </summary>
    public static IEnumerable<string> GetCandidateFilepaths(string filepath, AssetConfig config)
    {
        return config.Workspace.FindPossibleFilepaths(filepath);
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
            sourceFilePath = AppendFileVersion(sourceFilePath, config);
            return File.Exists(sourceFilePath) ? sourceFilePath : null;
        }

        if (sourceFilePath == null) return null;
        sourceFilePath = AppendFileVersion(sourceFilePath, config);
        sourceFilePath = GetFilepathWithoutNativesFolder(sourceFilePath);

        if (sourceFilePath.StartsWith('@')) {
            // what are these?
            sourceFilePath = sourceFilePath.Substring(1);
        }
        var defaultExtractedPath = Path.Combine(config.Paths.SourcePathOverride.NullIfEmpty() ?? config.Paths.ChunkPath, sourceFilePath);
        if (File.Exists(defaultExtractedPath)) return defaultExtractedPath;

        string? attemptedPath;
        foreach (var candidate in GetCandidateFilepaths(sourceFilePath, config)) {
            if (!string.IsNullOrEmpty(config.Paths.SourcePathOverride) && File.Exists(attemptedPath = Path.Combine(config.Paths.SourcePathOverride, sourceFilePath))) {
                return attemptedPath;
            }

            if (File.Exists(attemptedPath = Path.Combine(config.Paths.ChunkPath, sourceFilePath))) {
                return attemptedPath;
            }

            foreach (var extra in config.Paths.AdditionalPaths) {
                if (File.Exists(attemptedPath = Path.Combine(extra, sourceFilePath))) {
                    return attemptedPath;
                }
            }
        }

        if (autoExtract && !IsIgnoredFilepath(sourceFilePath, config) && FileUnpacker.TryExtractFile(sourceFilePath, config) && File.Exists(attemptedPath = Path.Combine(config.Paths.ChunkPath, sourceFilePath))) {
            return attemptedPath;
        }

        return null;
    }

    public static string? ResolveExportPath(string? basePath, string? assetPath, SupportedGame game)
    {
        if (Path.IsPathRooted(basePath) && IsSameExtension(basePath, assetPath)) return basePath;
        if (assetPath?.StartsWith("res://") == true) {
            if (basePath == null || basePath.StartsWith(ProjectSettings.LocalizePath("res://"))) {
                assetPath = ProjectSettings.GlobalizePath(assetPath);
            } else {
                var assetBase = PathUtils.GuessAssetConfigFromImportPath(assetPath);
                assetPath = ProjectSettings.GlobalizePath(assetPath);
                if (assetBase != null) {
                    assetPath = assetPath.Replace(assetBase.ImportBasePath, "");
                }
            }
        }

        if (!Path.IsPathRooted(assetPath)) {
            if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(basePath)) {
                return null;
            }

            assetPath = Path.Combine(basePath, assetPath);
        }

        var config = ReachForGodot.GetAssetConfig(game) ?? throw new Exception("Missing config for game " + game);
        return PathUtils.AppendFileVersion(assetPath, config);
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
    [return: NotNullIfNotNull(nameof(filepath))]
    public static string? NormalizeFilePath(string? filepath)
    {
        return filepath?.Replace('\\', '/');
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

        if (fullSourcePath.StartsWith(config.ImportBasePath)) {
            return config.ImportBasePath;
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
        if (relativePath.StartsWith("res://")) relativePath = relativePath.Replace("res://", "");
        if (relativePath.Contains('/') && relativePath[..relativePath.IndexOf('/')].Contains(config.Paths.ShortName, StringComparison.OrdinalIgnoreCase)) {
            relativePath = relativePath[(relativePath.IndexOf('/') + 1)..];
        }
        if (relativePath.StartsWith('/')) relativePath = relativePath.Substring(1);
        return GetFilepathWithoutVersion(relativePath);
    }

    public static string? ImportPathToRelativePath(string importPath)
    {
        if (string.IsNullOrEmpty(importPath)) return null;

        var globalizedImport = ProjectSettings.GlobalizePath(importPath);
        foreach (var config in ReachForGodot.AssetConfigs) {
            if (globalizedImport.StartsWith(config.ImportBasePath)) {
                return ImportPathToRelativePath(importPath, config);
            }
        }

        GD.PrintErr($"Could not resolve asset expected relative filepath, consider moving it into an AssetConfig's import folder.\nPath: {importPath}");
        return importPath.Replace("res://", "");
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

    private static string? FullOrRelativePathToImportPath(string sourcePath, KnownFileFormats fmt, AssetConfig config, bool resource)
    {
        var relativePath = Path.IsPathRooted(sourcePath) ? FullToRelativePath(sourcePath, config) : sourcePath;
        if (relativePath == null) return null;

        sourcePath = GetFilepathWithoutVersion(relativePath);

        if (!sourcePath.StartsWith("res://")) {
            sourcePath =  ProjectSettings.LocalizePath(Path.Combine(config.AssetDirectory, sourcePath));
        }
        if (!resource) {
            switch (fmt) {
                case KnownFileFormats.Mesh:
                    return sourcePath + ".glb";
                case KnownFileFormats.Texture:
                    return sourcePath + ".dds";
                case KnownFileFormats.CollisionMesh:
                case KnownFileFormats.RequestSetCollider:
                case KnownFileFormats.Scene:
                case KnownFileFormats.Prefab:
                case KnownFileFormats.Effect:
                    return sourcePath + ".tscn";
                default:
                    return sourcePath + ".tres";
            }
        }

        return sourcePath + ".tres";
    }
}