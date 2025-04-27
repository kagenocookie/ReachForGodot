namespace CustomFileBrowser;

public class FileListFileSystem : ICustomFileSystem
{
    public string[] Files { get; private set; } = Array.Empty<string>();

    private Dictionary<string, string[]> folderListCache = new();

    private PathFilter? _filter;
    public PathFilter? Filter
    {
        get => _filter;
        set {
            _filter = value;
            folderListCache.Clear();
        }
    }

    public delegate bool PathFilter(ReadOnlySpan<char> path);

    public IEnumerable<string> GetRecursiveFileList(string folder, int limit = -1)
    {
        var list = new List<string>();
        var queueFolders = new Queue<string>();
        queueFolders.Enqueue(folder);
        while (queueFolders.Count > 0) {
            foreach (var file in GetFilesInFolder(queueFolders.Dequeue())) {
                if (Path.GetExtension(file.AsSpan()).IsEmpty) {
                    queueFolders.Enqueue(file);
                } else if (Filter?.Invoke(file) != false) {
                    list.Add(file);
                    if (limit > 0 && list.Count >= limit) return list;
                }
            }
        }

        return list;
    }
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

        if (startIndex >= 0 && startIndex < Files.Length && Filter?.Invoke(Files[startIndex]) != false) {
            list.Add(GetSubfolderPath(Files[startIndex], folderNormalized).ToString());
        }
        int endIndex = startIndex + 1;
        while (endIndex < count) {
            var next = Files[endIndex++];
            if (!next.StartsWith(folderNormalized)) break;
            if ((list.Count == 0 || !next.StartsWith(list.Last())) && Filter?.Invoke(next) != false) {
                list.Add(GetSubfolderPath(next, folderNormalized).ToString());
            }
        }

        return folderListCache[folderNormalized] = names = list.ToArray();
    }

    private static ReadOnlySpan<char> GetSubfolderPath(string path, string fromFolder)
    {
        var nextSlash = fromFolder.EndsWith('/') ? path.IndexOf('/', fromFolder.Length) : path.IndexOf('/', fromFolder.Length + 1);
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
