using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Godot;

namespace RFG;

public class Importer
{
    private const string meshImportScriptPath = "addons/ReachForGodot/import_mesh.py";
    private const string texImportScriptPath = "addons/ReachForGodot/import_tex.py";

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
        if (fmt == RESupportedFileFormats.Unknown) return REFileFormat.Unknown;

        return new REFileFormat(fmt, version);
    }

    public static RESupportedFileFormats GetFileFormatFromExtension(ReadOnlySpan<char> extension)
    {
        if (extension.SequenceEqual("mesh")) {
            return RESupportedFileFormats.Mesh;
        }

        if (extension.SequenceEqual("tex")) {
            return RESupportedFileFormats.Texture;
        }

        if (extension.SequenceEqual("scn")) {
            return RESupportedFileFormats.Scene;
        }

        return RESupportedFileFormats.Unknown;
    }

    public static string GetFileExtensionFromFormat(RESupportedFileFormats format) => format switch {
        RESupportedFileFormats.Mesh => "mesh",
        RESupportedFileFormats.Texture => "tex",
        RESupportedFileFormats.Scene => "scn",
        _ => string.Empty,
    };

    public static int GuessFileVersion(string relativePath, RESupportedFileFormats format, AssetConfig config)
    {
        switch (format) {
            case RESupportedFileFormats.Mesh:
                break;
        }

        var fullpath = Path.Join(config.Paths.ChunkPath, relativePath);
        var ext = GetFileExtensionFromFormat(format);
        var first = Directory.EnumerateFiles(fullpath.GetBaseDir(), $"*.{ext}.*").FirstOrDefault();
        if (first != null) {
            return int.TryParse(first.GetExtension(), out var ver) ? ver : -1;
        }
        return -1;
    }

    public static string GetDefaultImportPath(string osFilepath, AssetConfig config)
    {
        return ProjectSettings.LocalizePath(GetDefaultImportPath(osFilepath, GetFileFormat(osFilepath), config));
    }

    public static string GetDefaultImportPath(string osFilepath, REFileFormat fmt, AssetConfig config)
    {
        var basepath = ReachForGodot.GetChunkPath(config.Game);
        if (basepath == null) {
            throw new ArgumentException($"{config.Game} chunk path not configured");
        }
        var relativePath = osFilepath.Replace(basepath, "");
        var realOsFilepath = ResolveSourceFilePath(relativePath, config);

        relativePath = realOsFilepath.Replace(basepath, "");
        var targetPath = Path.Combine(config.AssetDirectory, relativePath);

        switch (fmt.format) {
            case RESupportedFileFormats.Mesh:
                return targetPath + ".blend";
            case RESupportedFileFormats.Texture:
                return targetPath + ".dds";
            case RESupportedFileFormats.Scene:
                return targetPath + ".tscn";
            default:
                return targetPath;
        }
    }

    public static string ResolveSourceFilePath(string assetRelativePath, AssetConfig config)
    {
        var fmt = GetFileFormat(assetRelativePath);
        string extractedFilePath;
        if (fmt.version == -1) {
            fmt.version = GuessFileVersion(assetRelativePath, fmt.format, config);
            extractedFilePath = Path.Join(config.Paths.ChunkPath, assetRelativePath + "." + fmt.version).Replace('\\', '/');
        } else {
            extractedFilePath = Path.Join(config.Paths.ChunkPath, assetRelativePath).Replace('\\', '/');
        }

        return extractedFilePath;
    }

    public static Task Import(string filepath, string? importFilepath = null, AssetConfig? config = null)
    {
        config ??= AssetConfig.DefaultInstance;
        importFilepath ??= GetDefaultImportPath(filepath, config);
        var format = Importer.GetFileFormat(filepath);
        Directory.CreateDirectory(ProjectSettings.GlobalizePath(config.AssetDirectory));
        return Importer.Import(format, filepath, importFilepath, config);
    }

    public static Task Import(REFileFormat format, string sourceFilePath, string outputFilePath, AssetConfig? config = null)
    {
        config ??= AssetConfig.DefaultInstance;
        switch (format.format) {
            case RESupportedFileFormats.Mesh:
                return ImportMesh(sourceFilePath, outputFilePath);
            case RESupportedFileFormats.Texture:
                return ImportTexture(sourceFilePath, outputFilePath);
            case RESupportedFileFormats.Scene:
                return ImportScene(sourceFilePath, outputFilePath, config);
            default:
                GD.Print("Unsupported file format " + format.format);
                return Task.CompletedTask;
        }
    }

    private static byte[] MPLY_mesh_bytes = Encoding.ASCII.GetBytes("MPLY");

    public static Task ImportMesh(string sourceFilePath, string importFilepath)
    {
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
                GD.Print("Unsupported MPLY mesh " + sourceFilePath);
                return Task.CompletedTask;
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
            }
        });
    }

    public static Task ImportTexture(string sourceFilePath, string outputFilePath)
    {
        var path = Directory.GetCurrentDirectory();
        var outputGlobalized = ProjectSettings.GlobalizePath(outputFilePath);
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
            File.Move(convertedFilepath, outputGlobalized, true);
        });
    }

    public static Task ImportScene(string sourceFilePath, string outputFilePath, AssetConfig config)
    {
        var conv = new GodotScnConverter(config, false);
        conv.CreateProxyScene(sourceFilePath, outputFilePath);
        return Task.CompletedTask;
    }

    private static Task ExecuteBlenderScript(string scriptFilename, bool background)
    {
        var process = Process.Start(new ProcessStartInfo() {
            UseShellExecute = false,
            FileName = ReachForGodot.BlenderPath,
            Arguments = background ? $"--background --python \"{scriptFilename}\"" : $"--python \"{scriptFilename}\"",
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
}

public record struct REFileFormat(RESupportedFileFormats format, int version)
{
    public static readonly REFileFormat Unknown = new REFileFormat(RESupportedFileFormats.Unknown, -1);
}