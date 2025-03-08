namespace ReaGE;

using System;
using Godot;

public interface IRszContainerNode
{
    public SupportedGame Game { get; set; }
    public AssetReference? Asset { get; set; }
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
        var path = resource.Asset?.AssetFilename;
        if (string.IsNullOrEmpty(path)) return false;
        if (Resources == null) {
            Resources = new[] { resource };
            return true;
        }

        foreach (var res in Resources) {
            if (res == resource) return false;
        }

        Resources = Resources.Append(resource).ToArray();
        return true;
    }
}