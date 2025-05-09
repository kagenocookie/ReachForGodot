namespace ReaGE;
public interface IAssetPointer
{
    public SupportedGame Game { get; set; }
    public AssetReference? Asset { get; set; }
}

public interface IExportableAsset : IAssetPointer { }

public interface IImportableAsset : IAssetPointer
{
    bool IsEmpty { get; }
    IEnumerable<(string label, GodotImportOptions importMode)> SupportedImportTypes => [("Full reimport", GodotImportOptions.fullReimport)];
}

public interface IRszContainer : IExportableAsset
{
    public REResource[]? Resources { get; set; }
    /// <summary>
    /// Returns a user friendly identifiable full path to the target object.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Returns whether the object is empty with no content data loaded in yet (meaning, only a placeholder)
    /// </summary>
    public bool IsEmpty { get; }

    public T? FindResource<T>(string? filepath) where T : REResource
    {
        if (Resources == null || string.IsNullOrEmpty(filepath)) return null;
        foreach (var res in Resources) {
            if (res is T cast && cast.Asset?.IsSameAsset(filepath) == true) {
                return cast;
            }
        }
        return null;
    }

    public bool EnsureContainsResource(REResource resource)
    {
        var path = resource.Asset?.ExportedFilename;
        if (string.IsNullOrEmpty(path)) return false;
        if (Resources == null) {
            Resources = new[] { resource };
            return true;
        }

        foreach (var res in Resources) {
            if (true == res.Asset?.ExportedFilename.Equals(path, StringComparison.OrdinalIgnoreCase)) return false;
        }

        Resources = Resources.Append(resource).ToArray();
        return true;
    }
}
