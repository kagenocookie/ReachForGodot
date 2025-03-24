namespace CustomFileBrowser;

public interface ICustomFileSystem
{
    string[] GetFilesInFolder(string folder);
    PathType GetPathType(string path);
    string? GetPathInfo(string path, string field);
}

public enum PathType
{
    File,
    Folder,
}