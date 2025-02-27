using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Tasks;
using Godot;

namespace RGE;

public class Importer
{
    private const string meshImportScriptPath = "addons/ReachForGodot/import_mesh.py";
    private const string texImportScriptPath = "addons/ReachForGodot/import_tex.py";

    private static readonly byte[] MPLY_mesh_bytes = Encoding.ASCII.GetBytes("MPLY");

    public static REResource? FindImportedResourceAsset(Resource? asset)
    {
        if (asset == null) return null;
        if (asset is REResource reres) return reres;

        var path = asset.ResourcePath;
        if (asset is PackedScene ps) {
            // could be tscn (pfb, scn) or a mesh (.blend)
            if (path.EndsWith(".blend")) {
                return FindImportedResourceAsset(path);
            }
        }

        throw new ArgumentException("Unsupported asset resource " + path, nameof(asset));
    }

    public static REResource? FindImportedResourceAsset(string? resourceFilepath)
    {
        if (string.IsNullOrEmpty(resourceFilepath)) return null;

        if (resourceFilepath.EndsWith(".blend")) {
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
            var sourcePath = PathUtils.ResolveSourceFilePath(sourceFile, config);
            if (sourcePath == null) return false;
            Importer.Import(sourcePath, config, importPath);
            return true;
        }

        return true;
    }

    public static bool CheckResourceExists(string sourceFile, AssetConfig config)
    {
        if (string.IsNullOrEmpty(sourceFile)) {
            return false;
        }

        var importPath = PathUtils.GetLocalizedImportPath(sourceFile, config);
        if (importPath == null) return false;
        if (ResourceLoader.Exists(importPath)) return true;

        var sourcePath = PathUtils.ResolveSourceFilePath(sourceFile, config);
        return File.Exists(sourcePath);
    }

    /// <summary>
    /// Fetch an existing resource, or if it doesn't exist yet, create a placeholder resource for it.
    /// </summary>
    public static T? FindOrImportResource<T>(string chunkRelativeFilepath, AssetConfig config) where T : Resource
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

        if (!ResourceLoader.Exists(importPath)) {
            var sourcePath = PathUtils.ResolveSourceFilePath(chunkRelativeFilepath, config);
            if (sourcePath == null) return null;
            return Importer.Import(sourcePath, config, importPath) as T;
        }

        return ResourceLoader.Load<T>(importPath);
    }

    public static Resource? Import(string sourceFilePath, AssetConfig config, string? importFilepath = null)
    {
        importFilepath ??= PathUtils.GetLocalizedImportPath(sourceFilePath, config);
        var format = PathUtils.GetFileFormat(sourceFilePath);
        var outputFilePath = ProjectSettings.GlobalizePath(importFilepath);

        switch (format.format) {
            case RESupportedFileFormats.Mesh:
                return ImportResource<MeshResource>(sourceFilePath, outputFilePath, config);
            case RESupportedFileFormats.Texture:
                return ImportResource<TextureResource>(sourceFilePath, outputFilePath, config);
            case RESupportedFileFormats.Scene:
                return ImportScene(sourceFilePath, outputFilePath, config);
            case RESupportedFileFormats.Prefab:
                return ImportPrefab(sourceFilePath, outputFilePath, config);
            case RESupportedFileFormats.Userdata:
                return ImportUserdata(sourceFilePath, outputFilePath, config);
            default:
                return ImportResource<REResource>(sourceFilePath, outputFilePath, config);
        }
    }

    public static Task<bool>? ImportMesh(string? sourceFilePath, SupportedGame game)
    {
        var config = ReachForGodot.GetAssetConfig(game);
        var importFilepath = PathUtils.GetAssetImportPath(sourceFilePath, RESupportedFileFormats.Mesh, config);
        if (!System.IO.Path.IsPathRooted(sourceFilePath)) {
            sourceFilePath = PathUtils.ResolveSourceFilePath(sourceFilePath, config);
        }
        if (sourceFilePath == null) return Task.FromResult(false);

        var path = Directory.GetCurrentDirectory();
        var outputGlobalized = ProjectSettings.GlobalizePath(importFilepath);
        var blendPath = Path.GetFullPath(outputGlobalized).Replace('\\', '/');
        var importDir = Path.GetFullPath(outputGlobalized.GetBaseDir());
        // GD.Print($"Importing mesh...\nBlender: {ReachForGodot.BlenderPath}\nFile: {path}\nTarget: {blendPath}\nPython script: {meshImportScriptPath}");

        Directory.CreateDirectory(importDir);
        using (var meshPreview = File.OpenRead(sourceFilePath)) {
            var bytes = new byte[4];
            meshPreview.ReadExactly(bytes);
            if (bytes.AsSpan().SequenceEqual(MPLY_mesh_bytes)) {
                GD.PrintErr("Unsupported MPLY mesh " + sourceFilePath);
                return null;
            }
        }

        var importScript = File.ReadAllText(meshImportScriptPath)
            .Replace("__FILEPATH__", sourceFilePath)
            .Replace("__FILEDIR__", sourceFilePath.GetBaseDir())
            .Replace("__FILENAME__", sourceFilePath.GetFile())
            .Replace("__OUTPUT_PATH__", blendPath);

        var tempFn = Path.GetTempFileName();
        using var tmpfile = File.Create(tempFn);
        tmpfile.Write(importScript.ToUtf8Buffer());
        tmpfile.Flush();
        tmpfile.Close();

        return ExecuteBlenderScript(tempFn, false).ContinueWith((_) => {
            if (!File.Exists(blendPath)) {
                GD.Print("Unsuccessfully imported mesh " + sourceFilePath);
                return false;
            }

            QueueFileRescan();
            return true;
        });
    }

    public static Task<bool>? ImportTexture(string? sourceFilePath, SupportedGame game)
    {
        var config = ReachForGodot.GetAssetConfig(game);
        var importFilepath = PathUtils.GetAssetImportPath(sourceFilePath, RESupportedFileFormats.Texture, config);
        if (!System.IO.Path.IsPathRooted(sourceFilePath)) {
            sourceFilePath = PathUtils.ResolveSourceFilePath(sourceFilePath, config);
        }
        if (sourceFilePath == null) return Task.FromResult(false);

        var path = Directory.GetCurrentDirectory();
        var outputGlobalized = ProjectSettings.GlobalizePath(importFilepath);
        var importDir = Path.GetFullPath(outputGlobalized.GetBaseDir());
        var convertedFilepath = sourceFilePath.GetBaseName().GetBaseName() + ".dds";

        Directory.CreateDirectory(importDir);

        var importScript = File.ReadAllText(texImportScriptPath)
            .Replace("__FILEPATH__", sourceFilePath)
            .Replace("__FILEDIR__", sourceFilePath.GetBaseDir())
            .Replace("__FILENAME__", sourceFilePath.GetFile());

        var tempFn = Path.GetTempFileName();
        using var tmpfile = File.Create(tempFn);
        tmpfile.Write(importScript.ToUtf8Buffer());
        tmpfile.Flush();
        tmpfile.Close();

        return ExecuteBlenderScript(tempFn, true).ContinueWith((_) => {
            if (File.Exists(convertedFilepath)) {
                File.Move(convertedFilepath, outputGlobalized, true);
                QueueFileRescan();
                return true;
            } else {
                // array textures and supported stuff... not sure how to handle those
                return false;
            }
        });
    }

    public static PackedScene? ImportScene(string? sourceFilePath, string outputFilePath, AssetConfig config)
    {
        if (!System.IO.Path.IsPathRooted(sourceFilePath)) {
            sourceFilePath = PathUtils.ResolveSourceFilePath(sourceFilePath, config);
        }
        if (!File.Exists(sourceFilePath)) {
            GD.PrintErr("Scene file not found: " + sourceFilePath);
            return null;
        }
        var conv = new RszGodotConverter(config, RszGodotConverter.placeholderImport);
        return conv.CreateOrReplaceScene(sourceFilePath, outputFilePath);
    }

    public static PackedScene? ImportPrefab(string? sourceFilePath, string outputFilePath, AssetConfig config)
    {
        if (!System.IO.Path.IsPathRooted(sourceFilePath)) {
            sourceFilePath = PathUtils.ResolveSourceFilePath(sourceFilePath, config);
        }
        if (!File.Exists(sourceFilePath)) {
            GD.PrintErr("Prefab file not found: " + sourceFilePath);
            return null;
        }
        var conv = new RszGodotConverter(config, RszGodotConverter.placeholderImport);
        return conv.CreateOrReplacePrefab(sourceFilePath, outputFilePath);
    }

    public static UserdataResource? ImportUserdata(string? sourceFilePath, string outputFilePath, AssetConfig config)
    {
        if (!System.IO.Path.IsPathRooted(sourceFilePath)) {
            sourceFilePath = PathUtils.ResolveSourceFilePath(sourceFilePath, config);
        }
        if (!File.Exists(sourceFilePath)) {
            GD.PrintErr("Userdata file not found: " + sourceFilePath);
            return null;
        }
        var conv = new RszGodotConverter(config, RszGodotConverter.placeholderImport);
        return conv.CreateOrReplaceUserdata(sourceFilePath, outputFilePath);
    }

    public static void QueueFileRescan()
    {
        var fs = EditorInterface.Singleton.GetResourceFilesystem();
        if (!fs.IsScanning()) fs.CallDeferred(EditorFileSystem.MethodName.Scan);
    }

    private static T? ImportResource<T>(string? sourceFilePath, string outputFilePath, AssetConfig config)
        where T : REResource, new()
    {
        if (string.IsNullOrEmpty(sourceFilePath)) return null;
        if (!System.IO.Path.IsPathRooted(sourceFilePath)) {
            var resolved = PathUtils.ResolveSourceFilePath(sourceFilePath, config);
            if (resolved != null) {
                sourceFilePath = resolved;
            }
        }

        RESupportedFileFormats format;
        if (!File.Exists(sourceFilePath)) {
            GD.PrintErr("Resource file not found: " + sourceFilePath);
            format = RESupportedFileFormats.Unknown;
            sourceFilePath = Path.GetRelativePath(config.Paths.ChunkPath, sourceFilePath);
        } else {
            format = PathUtils.GetFileFormat(sourceFilePath).format;
            sourceFilePath = Path.GetRelativePath(config.Paths.ChunkPath, sourceFilePath);
        }

        Directory.CreateDirectory(outputFilePath.GetBaseDir());
        var newres = new T() {
            Asset = new AssetReference(sourceFilePath),
            ResourceType = format,
            Game = config.Game,
            ResourceName = sourceFilePath.GetFile(),
            ResourcePath = ProjectSettings.LocalizePath(outputFilePath),
        };
        ResourceSaver.Save(newres);
        // if we later end up adding a proper resource type, call newres.TakeOverPath() to replace the placeholder instance
        // QueueFileRescan();
        return newres;
    }

    private static Task ExecuteBlenderScript(string scriptFilename, bool background)
    {
        var process = Process.Start(new ProcessStartInfo() {
            UseShellExecute = false,
            FileName = ReachForGodot.BlenderPath,
            WindowStyle = ProcessWindowStyle.Hidden,
            Arguments = background ? $"--no-window-focus --background --python \"{scriptFilename}\"" : $"--no-window-focus --python \"{scriptFilename}\"",
        });

        return process!.WaitForExitAsync();
    }
}

public enum RESupportedFileFormats
{
    Unknown,
    Mesh,
    Texture,
    Scene,
    Prefab,
    Userdata,
}

public record struct REFileFormat(RESupportedFileFormats format, int version)
{
    public static readonly REFileFormat Unknown = new REFileFormat(RESupportedFileFormats.Unknown, -1);
}