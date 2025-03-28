using Godot;

namespace CustomFileBrowser;

public class FileListFileSystem : ICustomFileSystem
{
    private string[] Files = Array.Empty<string>();

    private Dictionary<string, string[]> folderListCache = new();

    public FileListFileSystem()
    {
    }

    public FileListFileSystem(string fileList)
    {
        ReadFileList(fileList);

    }
    public FileListFileSystem(string[] files)
    {
        Files = files.Select(f => NormalizePath(f).ToLowerInvariant()).Order().ToArray();
    }

    public void ReadFileList(string listFilepath)
    {
        if (!File.Exists(listFilepath)) {
            throw new ArgumentException("List file does not exist", nameof(listFilepath));
        }

        using var f = new StreamReader(File.OpenRead(listFilepath));
        var list = new SortedSet<string>();
        while (!f.EndOfStream) {
            var line = f.ReadLine();
            if (!string.IsNullOrWhiteSpace(line)) {
                var norm = NormalizePath(line);
                list.Add(norm);
            }
        }

        Files = list.ToArray();
    }

    public string[] GetFilesInFolder(string folder)
    {
        folder = NormalizePath(folder);
        var lower = folder.ToLowerInvariant();
        if (string.IsNullOrEmpty(lower)) {
            return GetFolderFileNames(string.Empty);
        }
        return GetFolderFileNames(lower + "/");
    }

    private string[] GetFolderFileNames(string folderNormalized)
    {
        if (folderListCache.TryGetValue(folderNormalized, out var names)) {
            return names;
        }

        var startIndex = Array.BinarySearch<string>(Files, folderNormalized);
        if (startIndex < 0) {
            startIndex = ~startIndex;
            if (startIndex > Files.Length) return Array.Empty<string>();
        }
        // TODO will have to handle invalid filepaths here as well - maybe a StartsWith check
        var count = Files.Length;
        var list = new List<string>();

        list.Add(GetWithImmediateSubfolder(Files[startIndex], folderNormalized).ToString());
        int endIndex = startIndex + 1;
        while (endIndex < count) {
            var next = Files[endIndex++];
            if (!next.StartsWith(folderNormalized)) break;
            if (!next.StartsWith(list.Last())) {
                list.Add(GetWithImmediateSubfolder(next, folderNormalized).ToString());
            }
        }

        return folderListCache[folderNormalized] = names = list.ToArray();
    }

    private static ReadOnlySpan<char> GetWithImmediateSubfolder(string path, string relativeTo)
    {
        var nextSlash = relativeTo.EndsWith('/') ? path.IndexOf('/', relativeTo.Length) : path.IndexOf('/', relativeTo.Length + 1);
        if (nextSlash == -1) return path;
        return path.AsSpan().Slice(0, nextSlash);
    }

    private static string NormalizePath(string path)
    {
        if (path.IndexOf('\\') != -1) path = path.Replace('\\', '/');
        if (path.StartsWith('/')) path = path.Substring(1);
        return path;
    }

    public virtual string? GetPathInfo(string path, string field)
    {
        if (field.Equals("path", StringComparison.OrdinalIgnoreCase)) return path;
        if (field.Equals("name", StringComparison.OrdinalIgnoreCase)) return Path.GetFileName(path);
        if (field.Equals("extension", StringComparison.OrdinalIgnoreCase)) return Path.GetExtension(path);
        if (field.Equals("type", StringComparison.OrdinalIgnoreCase)) return GetPathType(path).ToString();

        return null;
    }

    public PathType GetPathType(string path)
    {
        var exactMatch = Array.BinarySearch(Files, NormalizePath(path));
        return exactMatch < 0 ? PathType.Folder : PathType.File;
    }
}
