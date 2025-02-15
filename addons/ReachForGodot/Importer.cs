using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Godot;

namespace RFG;

public class Importer
{
    public static REFileFormat GetFileFormat(string filename)
    {
        var versionDot = filename.LastIndexOf('.');
        if (versionDot == -1) return REFileFormat.Unknown;

        var extDot = filename.LastIndexOf('.', versionDot - 1);
        if (extDot == -1) return REFileFormat.Unknown;

        if (!int.TryParse(filename.AsSpan().Slice(versionDot + 1), out var version)) {
            return REFileFormat.Unknown;
        }

        var ext = filename.AsSpan()[(extDot + 1)..versionDot];
        if (ext.SequenceEqual("mesh")) {
            return new REFileFormat(RESupportedFileFormats.Mesh, version);
        }

        if (ext.SequenceEqual("tex")) {
            return new REFileFormat(RESupportedFileFormats.Texture, version);
        }

        return REFileFormat.Unknown;
    }

    public static string GetDefaultImportPath(string filepath)
    {
        var basepath = ReachForGodot.GetChunkPath(AssetConfig.Instance.Game);
        if (basepath == null) {
            throw new ArgumentException($"{AssetConfig.Instance.Game} chunk path not configured");
        }
        var relativePath = filepath.Replace(basepath, "");
        var targetPath = Path.Combine(AssetConfig.Instance.AssetDirectory, relativePath);
        var fmt = GetFileFormat(filepath);

        switch (fmt.format) {
            case RESupportedFileFormats.Mesh:
                return targetPath + ".blend";
            case RESupportedFileFormats.Texture:
                return targetPath + ".dds";
            default:
                return targetPath;
        }
    }

    public static Task Import(string filepath, string? outputFilePath = null)
    {
        outputFilePath ??= GetDefaultImportPath(filepath);
        var format = Importer.GetFileFormat(filepath);
        Directory.CreateDirectory(ProjectSettings.GlobalizePath(AssetConfig.Instance.AssetDirectory));
        return Importer.Import(format, filepath, outputFilePath);
    }

    public static Task Import(REFileFormat format, string sourceFilePath, string outputFilePath)
    {
        switch (format.format) {
            case RESupportedFileFormats.Mesh:
                return ImportMesh(sourceFilePath, outputFilePath);
            case RESupportedFileFormats.Texture:
                return ImportTexture(sourceFilePath, outputFilePath);
            case RESupportedFileFormats.Unknown:
            default:
                GD.Print("Unsupported file format " + format.format);
                return Task.CompletedTask;
        }
    }

    private const string meshImportScriptPath = "addons/ReachForGodot/import_mesh.py";
    public static Task ImportMesh(string sourceFilePath, string outputFilePath)
    {
        var path = Directory.GetCurrentDirectory();
        var blendPath = Path.GetFullPath(outputFilePath).Replace('\\', '/');
        var importDir = Path.GetFullPath(outputFilePath.GetBaseDir());
        // GD.Print($"Importing mesh...\nBlender: {ReachForGodot.BlenderPath}\nFile: {path}\nTarget: {blendPath}\nPython script: {meshImportScriptPath}");

        Directory.CreateDirectory(importDir);

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

        return ExecuteBlenderScript(tempFn);
    }

    private const string texImportScriptPath = "addons/ReachForGodot/import_tex.py";
    public static Task ImportTexture(string sourceFilePath, string outputFilePath)
    {
        var path = Directory.GetCurrentDirectory();
        var importDir = Path.GetFullPath(outputFilePath.GetBaseDir());
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

        return ExecuteBlenderScript(tempFn).ContinueWith((_) => {
            File.Move(convertedFilepath, outputFilePath, true);
        });
    }

    private static Task ExecuteBlenderScript(string scriptFilename)
    {
        var process = Process.Start(new ProcessStartInfo() {
            UseShellExecute = false,
            FileName = ReachForGodot.BlenderPath,
            Arguments = $"--python \"{scriptFilename}\"",
        });

        return process!.WaitForExitAsync();
    }
}

public enum RESupportedFileFormats
{
    Unknown,
    Mesh,
    Texture,
}

public record struct REFileFormat(RESupportedFileFormats format, int version)
{
    public static readonly REFileFormat Unknown = new REFileFormat(RESupportedFileFormats.Unknown, -1);
}