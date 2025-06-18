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

    public static Node? FindRootRszOwnerNode(this Node node)
    {
        var sceneRoot = EditorInterface.Singleton.GetEditedSceneRoot();
        if (sceneRoot.IsAncestorOf(node) && sceneRoot is IRszContainer) {
            return sceneRoot;
        }
        var owner = FindRszOwnerNode(node);
        if (owner != null) {
            Node? nextOwner;
            do {
                nextOwner = FindRszOwnerNode(owner);
                if (nextOwner != null) owner = nextOwner;
            } while (nextOwner != null);
        }
        return owner;
    }

    public static string? NullIfEmpty(this string? str)
    {
        return string.IsNullOrEmpty(str) ? null : str;
    }

    public static string StringOrDefault(this ReadOnlySpan<char> str, string fallback)
    {
        return str.IsEmpty ? fallback : str.ToString();
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

    public static TResource? GetAsset<TResource>(this REResource resource, bool saveAssetToFilesystem = true) where TResource : Resource
    {
        if (resource is REResourceProxy proxy) {
            if (proxy.ImportedResource != null) return proxy.ImportedResource as TResource;
            // in case the asset was already imported but not saved properly on the proxy resource
            var path = PathUtils.GetAssetImportPath(resource.Asset?.AssetFilename, resource.ResourceType, ReachForGodot.GetAssetConfig(resource.Game));
            if (path != null && ResourceLoader.Exists(path)) {
                var asset = (proxy.ImportedResource = ResourceLoader.Load<TResource>(path)) as TResource;
                if (saveAssetToFilesystem && asset != null) {
                    resource.SaveOrReplaceResource(resource.ResourcePath);
                }
                return asset;
            }
        }

        return resource as TResource;
    }

    public static Vector3 VariantToVector3(this Variant variant) => variant.VariantType == Variant.Type.Vector4 ? variant.AsVector4().ToVector3() : variant.AsVector3();
    public static Quaternion VariantToQuaternion(this Variant variant) => variant.VariantType == Variant.Type.Vector4 ? variant.AsVector4().ToQuaternion() : variant.AsQuaternion();
}