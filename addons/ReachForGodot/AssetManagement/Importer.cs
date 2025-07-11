using Godot;
using ReeLib;

namespace ReaGE;

public class Importer
{
    private static readonly Dictionary<KnownFileFormats, Type> resourceTypes = new();

    private static readonly GodotImportOptions writeImport = new(RszImportType.Placeholders, RszImportType.Placeholders, RszImportType.Placeholders, RszImportType.Placeholders) { allowWriting = true };
    private static readonly GodotImportOptions nowriteImport = new(RszImportType.Placeholders, RszImportType.Placeholders, RszImportType.Placeholders, RszImportType.Placeholders) { allowWriting = false };

    public static REResource? FindImportedResourceAsset(string? resourceFilepath)
    {
        if (string.IsNullOrEmpty(resourceFilepath)) return null;

        if (resourceFilepath.EndsWith(".blend") || resourceFilepath.EndsWith(".glb")) {
            var resLocalized = ProjectSettings.LocalizePath(resourceFilepath.GetBaseName() + ".tres");
            if (ResourceLoader.Exists(resLocalized)) {
                return ResourceLoader.Load<REResource>(resLocalized);
            }
        } else if (resourceFilepath.EndsWith(".dds")) {
            var resLocalized = ProjectSettings.LocalizePath(resourceFilepath.GetBaseName() + ".tres");
            if (ResourceLoader.Exists(resLocalized)) {
                return ResourceLoader.Load<REResource>(resLocalized);
            }
        } else if (resourceFilepath.EndsWith(".tres")) {
            var resLocalized = ProjectSettings.LocalizePath(resourceFilepath);
            if (ResourceLoader.Exists(resLocalized)) {
                return ResourceLoader.Load<REResource>(resLocalized);
            }
        }

        var localized = ProjectSettings.LocalizePath(resourceFilepath + ".tres");
        if (ResourceLoader.Exists(localized)) {
            return ResourceLoader.Load<REResource>(localized);
        }

        throw new Exception("Unknown asset resource reference " + resourceFilepath);
    }

    public static bool CheckResourceExists(string sourceFile, AssetConfig config, bool tryExtract = true)
    {
        if (string.IsNullOrEmpty(sourceFile)) {
            return false;
        }

        var importPath = PathUtils.GetLocalizedImportPath(sourceFile, config);
        if (importPath == null) return false;
        if (ResourceLoader.Exists(importPath)) return true;

        return PathUtils.FindSourceFilePath(sourceFile, config, tryExtract) != null;
    }

    /// <summary>
    /// Get the corresponding asset of a resource. This should be either equal to the input REResource instance for data resources, or any of Godot's normal resources like PackedScene, mesh, texture, audio files
    /// </summary>
    public static T? FindOrImportAsset<T>(string? chunkRelativeFilepath, AssetConfig config, bool saveAssetToFilesystem = true) where T : Resource
    {
        var resource = FindOrImportResource<REResource>(chunkRelativeFilepath, config, saveAssetToFilesystem);
        var asset = resource?.GetAsset<T>(saveAssetToFilesystem);
        if (asset != null) return asset;

        if (resource is REResourceProxy proxy) {
            return proxy.GetOrCreatePlaceholder(saveAssetToFilesystem ? writeImport : nowriteImport) as T;
        }

        return null;
    }

    /// <summary>
    /// Fetch an existing resource, or if it doesn't exist yet, create a placeholder resource for it.
    /// </summary>
    public static T? FindOrImportResource<T>(string? chunkRelativeFilepath, AssetConfig config, bool saveAssetToFilesystem = true) where T : REResource
    {
        if (string.IsNullOrEmpty(chunkRelativeFilepath)) {
            GD.PrintErr("Empty import path for resource " + typeof(T) + ": " + chunkRelativeFilepath);
            return null;
        }

        var importPath = PathUtils.GetLocalizedImportPath(chunkRelativeFilepath, config);
        if (importPath == null) {
            GD.PushError("Could not find resource " + typeof(T) + ": " + chunkRelativeFilepath);
            return null;
        }

        if (ResourceLoader.Exists(importPath)) {
            var resource = ResourceLoader.Load(importPath);
            if (resource is T castResource) return castResource;
            GD.PrintErr("Failed to load imported resource, re-importing: " + importPath);
        }

        var sourcePath = PathUtils.FindSourceFilePath(chunkRelativeFilepath, config, saveAssetToFilesystem);
        if (sourcePath == null) {
            return Importer.ImportResource(PathUtils.RelativeToFullPath(chunkRelativeFilepath, config), config, importPath, saveAssetToFilesystem) as T;
        }
        return Importer.ImportResource(sourcePath, config, importPath, saveAssetToFilesystem) as T;
    }

    public static REResource? ImportResource(string sourceFilePath, AssetConfig config, string? importFilepath = null, bool saveAssetToFilesystem = true)
    {
        importFilepath ??= PathUtils.GetLocalizedImportPath(sourceFilePath, config);
        if (string.IsNullOrEmpty(importFilepath)) return null;

        var format = PathUtils.ParseFileFormat(sourceFilePath).format;
        var outputFilePath = ProjectSettings.GlobalizePath(importFilepath);

        var resolvedPath = PathUtils.FindSourceFilePath(sourceFilePath, config);
        if (resolvedPath == null) {
            if (saveAssetToFilesystem) GD.PrintErr("Resource not found: " + sourceFilePath);
            if (!Path.IsPathRooted(sourceFilePath)) {
                sourceFilePath = Path.Combine(config.Paths.ChunkPath, sourceFilePath);
            }
        }
        var options = saveAssetToFilesystem && resolvedPath != null ? writeImport : nowriteImport;

        sourceFilePath = resolvedPath ?? sourceFilePath;
        var newres = CreateResource(format, sourceFilePath, outputFilePath, config);
        if (newres == null || !options.allowWriting) return newres;

        var importPath = ProjectSettings.LocalizePath(outputFilePath);
        newres.SaveOrReplaceResource(importPath);
        return newres;
    }

    private static void TrySaveResource(Resource res, string? filepath)
    {
        var status = ResourceSaver.Save(res);
        if (status != Error.Ok) {
            GD.PrintErr($"Failed to save new imported resource ({status}): {filepath}");
        }
    }

    private static REResource? CreateResource(KnownFileFormats format, string sourceFilePath, string outputFilePath, AssetConfig config)
    {
        var relativePath = PathUtils.FullToRelativePath(sourceFilePath, config);
        if (relativePath == null) {
            GD.PrintErr("Could not guarantee correct relative path for file " + sourceFilePath);
            relativePath = sourceFilePath;
        }

        var newres = resourceTypes.GetValueOrDefault(format) is Type rt ? (REResource)Activator.CreateInstance(rt)! : new REResource();
        newres.Asset = new AssetReference(relativePath);
        newres.Game = config.Game;
        newres.ResourceName = newres.ResourceType is KnownFileFormats.Prefab or KnownFileFormats.Scene or KnownFileFormats.RequestSetCollider or KnownFileFormats.Effect
            ? newres.Asset.BaseFilename.ToString()
            : PathUtils.GetFilepathWithoutVersion(relativePath).GetFile();

        return newres;
    }

    internal static void RegisterResource(KnownFileFormats format, Type type)
    {
        resourceTypes[format] = type;
    }
}
