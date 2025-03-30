using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Godot;
using RszTool;

namespace ReaGE;

public class Importer
{
    private const string meshImportScriptPath = "addons/ReachForGodot/import_mesh.py";
    private const string texImportScriptPath = "addons/ReachForGodot/import_tex.py";
    private static readonly string EmptyBlend = ProjectSettings.GlobalizePath("res://addons/ReachForGodot/.gdignore/empty.blend");

    private static string? _meshScript;
    private static string MeshImportScript => _meshScript ??= File.ReadAllText(Path.Combine(ProjectSettings.GlobalizePath("res://"), meshImportScriptPath));

    private static string? _texScript;
    private static string TexImportScript => _texScript ??= File.ReadAllText(Path.Combine(ProjectSettings.GlobalizePath("res://"), texImportScriptPath));

    private static readonly byte[] MPLY_mesh_bytes = Encoding.ASCII.GetBytes("MPLY");

    private static CancellationTokenSource? cancellationTokenSource;
    private const int blenderTimeoutMs = 30000;

    private static bool _hasShownNoBlenderWarning = false;

    private static readonly Dictionary<RESupportedFileFormats, Type> resourceTypes = new();

    private static readonly GodotImportOptions writeImport = new(RszImportType.Placeholders, RszImportType.Placeholders, RszImportType.Placeholders, RszImportType.Placeholders) { allowWriting = true };
    private static readonly GodotImportOptions nowriteImport = new(RszImportType.Placeholders, RszImportType.Placeholders, RszImportType.Placeholders, RszImportType.Placeholders) { allowWriting = true };

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

    public static bool EnsureResourceImported(string? sourceFile, AssetConfig config)
    {
        if (string.IsNullOrEmpty(sourceFile)) {
            return false;
        }

        var importPath = PathUtils.GetLocalizedImportPath(sourceFile, config);
        if (!ResourceLoader.Exists(importPath)) {
            var sourcePath = PathUtils.FindSourceFilePath(sourceFile, config);
            if (sourcePath == null) return false;
            Importer.Import(sourcePath, config, importPath);
            return true;
        }

        return true;
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
    /// Fetch an existing resource, or if it doesn't exist yet, create a placeholder resource for it.
    /// </summary>
    public static T? FindOrImportResource<T>(string? chunkRelativeFilepath, AssetConfig config, bool saveAssetToFilesystem = true) where T : Resource
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
            try {
                var resource = ResourceLoader.Load<T>(importPath);
                if (resource != null) return resource;
                GD.PrintErr("Failed to load imported resource, re-importing: " + importPath);
            } catch (Exception e) {
                GD.PrintErr("Failed to load imported resource, re-importing: " + importPath, e);
            }
        }

        var sourcePath = PathUtils.FindSourceFilePath(chunkRelativeFilepath, config);
        if (sourcePath == null) {
            return Importer.Import(PathUtils.RelativeToFullPath(chunkRelativeFilepath, config), config, importPath, saveAssetToFilesystem) as T;
        }
        return Importer.Import(sourcePath, config, importPath, saveAssetToFilesystem) as T;
    }

    public static Resource? Import(string sourceFilePath, AssetConfig config, string? importFilepath = null, bool saveAssetToFilesystem = true)
    {
        importFilepath ??= PathUtils.GetLocalizedImportPath(sourceFilePath, config);
        if (string.IsNullOrEmpty(importFilepath)) return null;

        var format = PathUtils.GetFileFormat(sourceFilePath).format;
        var outputFilePath = ProjectSettings.GlobalizePath(importFilepath);

        var resolvedPath = PathUtils.FindSourceFilePath(sourceFilePath, config);
        if (resolvedPath == null) {
            GD.PrintErr("Resource not found: " + sourceFilePath);
        }
        var options = saveAssetToFilesystem && resolvedPath != null ? writeImport : nowriteImport;

        switch (format) {
            case RESupportedFileFormats.Scene:
                return ImportScene(resolvedPath ?? sourceFilePath, outputFilePath, config, options);
            case RESupportedFileFormats.Prefab:
                return ImportPrefab(resolvedPath ?? sourceFilePath, outputFilePath, config, options);
            default:
                return ImportResource(format, resolvedPath ?? sourceFilePath, outputFilePath, config, options);
        }
    }

    public static bool IsSupportedMeshFile(string? sourceFilePath, SupportedGame game)
    {
        if (string.IsNullOrEmpty(sourceFilePath)) return false;

        if (!System.IO.Path.IsPathRooted(sourceFilePath)) {
            sourceFilePath = PathUtils.FindSourceFilePath(sourceFilePath, ReachForGodot.GetAssetConfig(game));
        }
        if (string.IsNullOrEmpty(sourceFilePath)) return false;

        using var meshPreview = File.OpenRead(sourceFilePath);
        var bytes = new byte[4];
        meshPreview.ReadExactly(bytes);
        if (bytes.AsSpan().SequenceEqual(MPLY_mesh_bytes)) {
            return false;
        }
        // empty occlusion or whatever meshes, we can't really import them since they're empty and/or non-existent
        if (sourceFilePath.Contains("occ.mesh.", StringComparison.OrdinalIgnoreCase) || sourceFilePath.Contains("occl.mesh.", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }
        return true;
    }

    public static Task<bool>? ImportMesh(string? sourceFilePath, SupportedGame game)
    {
        var config = ReachForGodot.GetAssetConfig(game);
        var importFilepath = PathUtils.GetAssetImportPath(sourceFilePath, RESupportedFileFormats.Mesh, config);
        if (!System.IO.Path.IsPathRooted(sourceFilePath)) {
            sourceFilePath = PathUtils.FindSourceFilePath(sourceFilePath, config);
        }
        if (sourceFilePath == null || !IsSupportedMeshFile(sourceFilePath, game)) {
            GD.PrintErr("Unsupported mesh " + sourceFilePath);
            return Task.FromResult(false);
        }

        var path = Directory.GetCurrentDirectory();
        var outputGlobalized = ProjectSettings.GlobalizePath(importFilepath);
        var outputPath = PathUtils.NormalizeFilePath(Path.GetFullPath(outputGlobalized));
        var importDir = Path.GetFullPath(outputGlobalized.GetBaseDir());
        // GD.Print($"Importing mesh...\nBlender: {ReachForGodot.BlenderPath}\nFile: {path}\nTarget: {blendPath}\nPython script: {meshImportScriptPath}");

        // "80004002 No such interface supported" from texconv when we have it convert mesh textures in background ¯\_(ツ)_/¯
        var includeMaterials = ReachForGodot.IncludeMeshMaterial;

        Directory.CreateDirectory(importDir);
        var importScript = MeshImportScript
            .Replace("__FILEPATH__", sourceFilePath)
            .Replace("__FILEDIR__", sourceFilePath.GetBaseDir())
            .Replace("__FILENAME__", sourceFilePath.GetFile())
            .Replace("__OUTPUT_PATH__", outputPath)
            .Replace("__INCLUDE_MATERIALS__", includeMaterials ? "True" : "False")
            ;

        return ExecuteBlenderScript(importScript, !includeMaterials).ContinueWith((t) => {
            if (!t.IsCompletedSuccessfully || !File.Exists(outputPath)) {
                GD.Print("Unsuccessfully imported mesh " + sourceFilePath);
                return false;
            }

            ForceEditorImportNewFile(outputPath);
            return true;
        });
    }

    public static Task<bool>? ImportTexture(string? sourceFilePath, SupportedGame game)
    {
        var config = ReachForGodot.GetAssetConfig(game);
        var importFilepath = PathUtils.GetAssetImportPath(sourceFilePath, RESupportedFileFormats.Texture, config);
        if (!System.IO.Path.IsPathRooted(sourceFilePath)) {
            sourceFilePath = PathUtils.FindSourceFilePath(sourceFilePath, config);
        }
        if (sourceFilePath == null) return Task.FromResult(false);

        var path = Directory.GetCurrentDirectory();
        var outputGlobalized = ProjectSettings.GlobalizePath(importFilepath);
        var importDir = Path.GetFullPath(outputGlobalized.GetBaseDir());
        var convertedFilepath = sourceFilePath.GetBaseName().GetBaseName() + ".dds";

        Directory.CreateDirectory(importDir);

        var importScript = TexImportScript
            .Replace("__FILEPATH__", sourceFilePath)
            .Replace("__FILEDIR__", sourceFilePath.GetBaseDir())
            .Replace("__FILENAME__", sourceFilePath.GetFile());

        return ExecuteBlenderScript(importScript, true).ContinueWith((t) => {
            if (t.IsCompletedSuccessfully && File.Exists(convertedFilepath)) {
                File.Move(convertedFilepath, outputGlobalized, true);
                QueueFileRescan();
                return true;
            } else {
                // array textures and supported stuff... not sure how to handle those
                return false;
            }
        });
    }

    private static PackedScene? ImportScene(string sourceFilePath, string outputFilePath, AssetConfig config, GodotImportOptions options)
    {
        var converter = new AssetConverter(config, options);
        return converter.Scn.CreateOrReplaceResourcePlaceholder(new AssetReference(PathUtils.FullToRelativePath(sourceFilePath, config)!));
    }

    private static PackedScene? ImportPrefab(string sourceFilePath, string outputFilePath, AssetConfig config, GodotImportOptions options)
    {
        var converter = new AssetConverter(config, options);
        return converter.Pfb.CreateOrReplaceResourcePlaceholder(new AssetReference(PathUtils.FullToRelativePath(sourceFilePath, config)!));
    }

    public static void QueueFileRescan()
    {
        var fs = EditorInterface.Singleton.GetResourceFilesystem();
        if (!fs.IsScanning()) fs.CallDeferred(EditorFileSystem.MethodName.Scan);
    }

    public static void ForceEditorImportNewFile(string file)
    {
        QueueFileRescan();
        // var fs = EditorInterface.Singleton.GetResourceFilesystem();
        // fs.CallDeferred(EditorFileSystem.MethodName.UpdateFile, file);
        // fs.CallDeferred(EditorFileSystem.MethodName.ReimportFiles, new Godot.Collections.Array<string>(new[] { file }));
    }

    private static REResource? ImportResource(RESupportedFileFormats format, string sourceFilePath, string outputFilePath, AssetConfig config, GodotImportOptions options)
    {
        var newres = CreateResource(format, sourceFilePath, outputFilePath, config);
        if (newres == null || !options.allowWriting) return newres;

        var importPath = ProjectSettings.LocalizePath(outputFilePath);
        if (ResourceLoader.Exists(importPath)) {
            newres.TakeOverPath(importPath);
        } else {
            newres.ResourcePath = importPath;
        }
        TrySaveResource(newres, sourceFilePath);
        return newres;
    }

    private static void TrySaveResource(Resource res, string? filepath)
    {
        var status = ResourceSaver.Save(res);
        if (status != Error.Ok) {
            GD.PrintErr($"Failed to save new imported resource ({status}): {filepath}");
        }
    }

    private static REResource? CreateResource(RESupportedFileFormats format, string sourceFilePath, string outputFilePath, AssetConfig config)
    {
        var relativePath = PathUtils.FullToRelativePath(sourceFilePath, config);
        if (relativePath == null) {
            GD.PrintErr("Could not guarantee correct relative path for file " + sourceFilePath);
            relativePath = sourceFilePath;
        }

        Directory.CreateDirectory(outputFilePath.GetBaseDir());
        var newres = resourceTypes.GetValueOrDefault(format) is Type rt ? (REResource)Activator.CreateInstance(rt)! : new REResource();
        newres.Asset = new AssetReference(relativePath);
        newres.Game = config.Game;
        newres.ResourceName = PathUtils.GetFilepathWithoutVersion(relativePath).GetFile();

        return newres;
    }

    private static async Task ExecuteBlenderScript(string script, bool background)
    {
        var blenderPath = ReachForGodot.BlenderPath;
        if (string.IsNullOrEmpty(blenderPath)) {
            if (!_hasShownNoBlenderWarning) {
                GD.PrintErr("Blender is not configured. Meshes and textures will not import.");
                _hasShownNoBlenderWarning = true;
            }
            return;
        }

        var process = Process.Start(new ProcessStartInfo() {
            UseShellExecute = false,
            FileName = blenderPath,
            Arguments = background
                ? $"\"{EmptyBlend}\" --background --python-expr \"{script}\""
                : $"\"{EmptyBlend}\" --python-expr \"{script}\"",
        });

        if (cancellationTokenSource == null || cancellationTokenSource.IsCancellationRequested) {
            cancellationTokenSource = new();
        }

        var delay = Task.Delay(blenderTimeoutMs, cancellationTokenSource.Token);
        var completedTask = await Task.WhenAny(process!.WaitForExitAsync(cancellationTokenSource.Token), delay);
        if (completedTask == delay) {
            cancellationTokenSource.Cancel();
            cancellationTokenSource = null;
        }
    }

    internal static void RegisterResource(RESupportedFileFormats format, Type type)
    {
        resourceTypes[format] = type;
    }
}

public record struct REFileFormat(RESupportedFileFormats format, int version)
{
    public static readonly REFileFormat Unknown = new REFileFormat(RESupportedFileFormats.Unknown, -1);
}