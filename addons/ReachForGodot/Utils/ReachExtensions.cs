using Godot;
using RszTool;

namespace ReaGE;

public static class ReachExtensions
{
    public static IRszContainer? FindRszOwner(this Node node)
    {
        if (node is IRszContainer rsz && rsz.Asset?.IsEmpty == false) {
            return rsz;
        }
        // ignore the owner node and check the hierarchy directly
        // this is to correctly handle objects added to EditableInstance nodes
        return node.FindNodeInParents<IRszContainer>(p => p.Asset?.IsEmpty == false)
            ?? node as IRszContainer
            ?? node.FindNodeInParents<IRszContainer>();
    }

    public static Node? FindRszOwnerNode(this Node node)
    {
        return FindRszOwner(node) as Node;
    }

    public static string StringOrDefault(this string? str, string fallback)
    {
        return string.IsNullOrEmpty(str) ? fallback : str;
    }

    public static void MarkSceneChangedIfChildOfActiveScene(this Node target)
    {
        var root = EditorInterface.Singleton.GetEditedSceneRoot();
        if (root != null && (root == target || root.IsAncestorOf(target))) {
            EditorInterface.Singleton.MarkSceneAsUnsaved();
        }
    }

    public static bool IsGameObjectRef(this RszField field)
    {
        return field.type == RszFieldType.GameObjectRef || field.type == RszFieldType.Uri && field.original_type.Contains("via.GameObjectRef");
    }

    public static TResource? GetAsset<TResource>(this REResource resource) where TResource : Resource
    {
        if (resource is REResourceProxy proxy) {
            if (proxy.ImportedResource != null) return proxy.ImportedResource as TResource;
            // in case the asset was already imported but not saved properly on the proxy resource
            var path = PathUtils.GetAssetImportPath(resource.Asset?.AssetFilename, resource.ResourceType, ReachForGodot.GetAssetConfig(resource.Game));
            if (path != null && ResourceLoader.Exists(path)) {
                return (proxy.ImportedResource = ResourceLoader.Load<TResource>(path)) as TResource;
            }
        }

        return resource as TResource;
    }
}