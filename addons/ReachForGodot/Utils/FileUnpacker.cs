namespace ReaGE;

using System.Text.RegularExpressions;
using RszTool;

public class FileUnpacker
{
    private static readonly HashSet<(SupportedGame, string)> attemptedFiles = new();

    public static bool TryExtractFile(string sourceFilePath, AssetConfig config)
    {
        // store list of accessed files, so we don't re-try the same missing pfb's several times per session
        if (!attemptedFiles.Add((config.Game, sourceFilePath))) return false;

        sourceFilePath = PathUtils.AppendFileVersion(sourceFilePath, config).ToLowerInvariant();
        var candidate = PathUtils.GetCandidateFilepaths(sourceFilePath, config);

        return TryExtractCustomFileList(candidate, config);
    }

    public static bool TryExtractCustomFileList(IEnumerable<string> files, AssetConfig config)
    {
        var reader = SetupPakReader(config.Paths.PakFiles);
        if (reader.PakFilePriority.Count == 0) return false;

        reader.AddFiles(files);
        return reader.UnpackFilesTo(ResolveOutput(config)) != 0;
    }

    public static bool TryExtractFilteredFiles(string regexFilter, AssetConfig config, List<string>? missingFiles = null)
    {
        var listfile = config.Paths.FilelistPath;
        if (string.IsNullOrEmpty(listfile)) return false;

        var reader = SetupPakReader(config.Paths.PakFiles);
        reader.AddFilesFromListFile(listfile);
        reader.Filter = new Regex(regexFilter);

        var count = reader.UnpackFilesTo(ResolveOutput(config), missingFiles);
        return count != 0;
    }

    private static PakReader SetupPakReader(string[] paklist)
    {
        var pakReader = new PakReader();
        pakReader.PakFilePriority = paklist.ToList();
        pakReader.MaxThreads = ReachForGodot.UnpackerMaxThreads;
        var failedFiles = new List<string>();
        return pakReader;
    }

    private static string ResolveOutput(AssetConfig config)
        => PathUtils.GetFilepathWithoutNativesFolder(config.Paths.ChunkPath);
}
