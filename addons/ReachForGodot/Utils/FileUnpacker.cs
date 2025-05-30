namespace ReaGE;

using System.Diagnostics;
using Godot;

public class FileUnpacker
{
    private static bool _hasWarnedMissingUnpackerConfig = false;
    private static bool _hasWarnedUnpackError = false;
    private static readonly HashSet<(SupportedGame, string)> attemptedFiles = new();

    public static bool TryExtractFile(string sourceFilePath, AssetConfig config)
    {
        // store list of accessed files, so we don't re-try the same missing pfb's several times per session
        if (!attemptedFiles.Add((config.Game, sourceFilePath))) return false;

        sourceFilePath = PathUtils.AppendFileVersion(sourceFilePath, config).ToLowerInvariant();
        var filepaths = PathUtils.GetCandidateFilepaths(sourceFilePath, config);
        return TryExtractCustomFileList(filepaths, config);
    }

    public static bool TryExtractCustomFileList(IEnumerable<string> files, AssetConfig config)
    {
        var tmpfn = Path.GetTempFileName();
        File.WriteAllLines(tmpfn, files);
        var success = FileUnpacker.TryExtractFilteredFiles(string.Empty, tmpfn, config);
        File.Delete(tmpfn);
        return success;
    }

    public static bool TryExtractFilteredFiles(string filter, AssetConfig config)
    {
        var listfile = config.Paths.FilelistPath;
        if (string.IsNullOrEmpty(listfile)) return false;
        return TryExtractFilteredFiles(filter, listfile, config);
    }

    public static bool TryExtractFilteredFiles(string filter, string listfile, AssetConfig config)
    {
        var paklist = config.Paths.PakFiles;
        if (paklist.Length == 0 || string.IsNullOrEmpty(listfile)) return false;
        var exePath = ReachForGodot.UnpackerExeFilepath;
        if (exePath == null) {
            if (!_hasWarnedMissingUnpackerConfig) {
                _hasWarnedMissingUnpackerConfig = true;
                GD.PrintErr("Can't auto-extract files from PAK. Please configure the unpacker exe filepath if you wish to resolve not-unpacked files");
            }
            return false;
        }
        var outputRoot = PathUtils.GetFilepathWithoutNativesFolder(config.Paths.ChunkPath);

        var argsBase = $"unpack -p \"{listfile}\" -i {{PAKFILE}} -o \"{outputRoot}\" -f \"{filter}\" --skip-unknown --override";

        // TODO: reverse order and remove --override flag?
        foreach (var pak in paklist) {
            var args = argsBase.Replace("{PAKFILE}", EscapeFilepathArgument(pak));
            var proc = Process.Start(new ProcessStartInfo(exePath) {
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });

            if (proc == null) {
                if (!_hasWarnedUnpackError) {
                    _hasWarnedUnpackError = true;
                    GD.PrintErr("Failed to execute unpacker");
                }
                return false;
            }

            proc.WaitForExit();
            if (proc.ExitCode != 0) {
                if (!_hasWarnedUnpackError) {
                    _hasWarnedUnpackError = true;
                    GD.PrintErr("Failed to auto-unpack files:\n" + proc.StandardOutput.ReadToEnd() + "\n\n" + proc.StandardError.ReadToEnd());
                }
                return false;
            }
        }

        return true;
    }

    private static string EscapeFilepathArgument(string arg)
    {
        return arg.StartsWith('"') ? arg : '"' + arg + '"';
    }
}