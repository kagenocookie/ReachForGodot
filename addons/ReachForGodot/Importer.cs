using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Godot;

namespace RGE;

public class Importer
{
    private const string meshImportScriptPath = "addons/ReachForGodot/import_mesh.py";
    private const string texImportScriptPath = "addons/ReachForGodot/import_tex.py";

    private static readonly byte[] MPLY_mesh_bytes = Encoding.ASCII.GetBytes("MPLY");

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
                    _ => 20,
                };
            case RESupportedFileFormats.Prefab:
                return config.Game switch {
                    SupportedGame.ResidentEvil7 => 16,
                    SupportedGame.ResidentEvil2 => 16,
                    SupportedGame.DevilMayCry5 => 16,
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
            return int.TryParse(first.GetExtension(), out var ver) ? ver : -1;
        }
        return -1;
    }

    public static bool EnsureResourceImported(string? sourceFile, AssetConfig config)
    {
        if (string.IsNullOrEmpty(sourceFile)) {
            return false;
        }

        var importPath = Importer.GetLocalizedImportPath(sourceFile, config);
        if (!ResourceLoader.Exists(importPath)) {
            var sourcePath = ResolveSourceFilePath(sourceFile, config);
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

        var importPath = Importer.GetLocalizedImportPath(sourceFile, config);
        if (importPath == null) return false;
        if (ResourceLoader.Exists(importPath)) return true;

        var sourcePath = ResolveSourceFilePath(sourceFile, config);
        return File.Exists(sourcePath);
    }

    /// <summary>
    /// Fetch an existing resource, or if it doesn't exist yet, create a placeholder resource for it.
    /// </summary>
    public static T? FindOrImportResource<T>(string sourceFile, AssetConfig config) where T : Resource
    {
        if (string.IsNullOrEmpty(sourceFile)) {
            GD.PrintErr("Empty import path for resource " + typeof(T) + ": " + sourceFile);
            return null;
        }

        var importPath = Importer.GetLocalizedImportPath(sourceFile, config);
        if (importPath == null) {
            GD.PushError("Could not find resource " + typeof(T) + ": " + sourceFile);
            return null;
        }

        if (!ResourceLoader.Exists(importPath)) {
            var sourcePath = ResolveSourceFilePath(sourceFile, config);
            if (sourcePath == null) return null;
            return Importer.Import(sourcePath, config, importPath) as T;
        }

        return ResourceLoader.Load<T>(importPath);
    }

    public static string? GetLocalizedImportPath(string osFilepath, AssetConfig config)
    {
        var path = GetDefaultImportPath(osFilepath, GetFileFormat(osFilepath).format, config, true);
        if (string.IsNullOrEmpty(path)) return null;

        return ProjectSettings.LocalizePath(path);
    }

    public static string? GetAssetImportPath(string? osFilepath, RESupportedFileFormats format, AssetConfig config)
    {
        if (osFilepath == null) return null;
        return ProjectSettings.LocalizePath(GetDefaultImportPath(osFilepath, format, config, false));
    }

    private static string? GetDefaultImportPath(string osFilepath, RESupportedFileFormats fmt, AssetConfig config, bool resource)
    {
        var basepath = ReachForGodot.GetChunkPath(config.Game);
        if (basepath == null) {
            throw new ArgumentException($"{config.Game} chunk path not configured");
        }
        var relativePath = osFilepath.Replace(basepath, "");
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

    public static string? ResolveSourceFilePath(string? assetRelativePath, AssetConfig config)
    {
        if (assetRelativePath == null) return null;
        var fmt = GetFileFormat(assetRelativePath);
        string extractedFilePath;
        if (fmt.version == -1) {
            fmt.version = GuessFileVersion(assetRelativePath, fmt.format, config);
            if (fmt.version == -1) {
                return null;
            }
            extractedFilePath = Path.Join(config.Paths.ChunkPath, assetRelativePath + "." + fmt.version).Replace('\\', '/');
        } else {
            extractedFilePath = Path.Join(config.Paths.ChunkPath, assetRelativePath).Replace('\\', '/');
        }

        return extractedFilePath;
    }

    public static Resource? Import(string sourceFilePath, AssetConfig config, string? importFilepath = null)
    {
        importFilepath ??= GetLocalizedImportPath(sourceFilePath, config);
        var format = Importer.GetFileFormat(sourceFilePath);
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
        var importFilepath = GetAssetImportPath(sourceFilePath, RESupportedFileFormats.Mesh, config);
        if (!System.IO.Path.IsPathRooted(sourceFilePath)) {
            sourceFilePath = ResolveSourceFilePath(sourceFilePath, config);
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
        var importFilepath = GetAssetImportPath(sourceFilePath, RESupportedFileFormats.Texture, config);
        if (!System.IO.Path.IsPathRooted(sourceFilePath)) {
            sourceFilePath = ResolveSourceFilePath(sourceFilePath, config);
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
            sourceFilePath = ResolveSourceFilePath(sourceFilePath, config);
        }
        if (!File.Exists(sourceFilePath)) {
            GD.PrintErr("Scene file not found: " + sourceFilePath);
            return null;
        }
        var conv = new RszGodotConverter(config, placeholderImport);
        return conv.CreateOrReplaceScene(sourceFilePath, outputFilePath);
    }

    private static RszGodotConversionOptions placeholderImport = new RszGodotConversionOptions(folders: RszImportType.Placeholders, meshes: RszImportType.Placeholders, prefabs: RszImportType.Placeholders);

    public static PackedScene? ImportPrefab(string? sourceFilePath, string outputFilePath, AssetConfig config)
    {
        if (!System.IO.Path.IsPathRooted(sourceFilePath)) {
            sourceFilePath = ResolveSourceFilePath(sourceFilePath, config);
        }
        if (!File.Exists(sourceFilePath)) {
            GD.PrintErr("Prefab file not found: " + sourceFilePath);
            return null;
        }
        var conv = new RszGodotConverter(config, placeholderImport);
        return conv.CreateOrReplacePrefab(sourceFilePath, outputFilePath);
    }

    public static UserdataResource? ImportUserdata(string? sourceFilePath, string outputFilePath, AssetConfig config)
    {
        if (!System.IO.Path.IsPathRooted(sourceFilePath)) {
            sourceFilePath = ResolveSourceFilePath(sourceFilePath, config);
        }
        if (!File.Exists(sourceFilePath)) {
            GD.PrintErr("Userdata file not found: " + sourceFilePath);
            return null;
        }
        var conv = new RszGodotConverter(config, placeholderImport);
        return conv.CreateOrReplaceUserdata(sourceFilePath, outputFilePath);
    }

    private static void QueueFileRescan()
    {
        var fs = EditorInterface.Singleton.GetResourceFilesystem();
        if (!fs.IsScanning()) fs.CallDeferred(EditorFileSystem.MethodName.Scan);
    }

    private static T? ImportResource<T>(string? sourceFilePath, string outputFilePath, AssetConfig config)
        where T : REResource, new()
    {
        if (!System.IO.Path.IsPathRooted(sourceFilePath)) {
            sourceFilePath = ResolveSourceFilePath(sourceFilePath, config);
        }
        if (!File.Exists(sourceFilePath)) {
            GD.PrintErr("Resource file not found: " + sourceFilePath);
            return null;
        }

        var format = Importer.GetFileFormat(sourceFilePath);
        sourceFilePath = Path.GetRelativePath(config.Paths.ChunkPath, sourceFilePath);
        Directory.CreateDirectory(outputFilePath.GetBaseDir());
        var newres = new T() {
            Asset = new AssetReference(sourceFilePath),
            ResourceType = format.format,
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