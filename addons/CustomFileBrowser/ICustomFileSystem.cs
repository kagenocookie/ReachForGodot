namespace CustomFileBrowser;

public interface ICustomFileSystem
{
    string[] GetFilesInFolder(string folder);
    PathType GetPathType(string path);
    string? GetPathInfo(string path, string field);

    IEnumerable<string> GetRecursiveFileList(string folder)
    {
        var list = new List<string>();
        var queueFolders = new Queue<string>();
        queueFolders.Enqueue(folder);
        while (queueFolders.Count > 0) {
            foreach (var file in GetFilesInFolder(queueFolders.Dequeue())) {
                if (Path.GetExtension(file.AsSpan()).IsEmpty) {
                    queueFolders.Enqueue(file);
                } else {
                    list.Add(file);
                }
            }
        }

        return list;
    }
}

public enum PathType
{
    File,
    Folder,
}