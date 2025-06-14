namespace ReaGE;

using Godot;

[GlobalClass, Tool, ResourceHolder("mcol", SupportedFileFormats.MeshCollider)]
public partial class MeshColliderResource : REResourceProxy, IExportableAsset
{
    public PackedScene? ImportedMesh => ImportedResource as PackedScene;
    public McolRoot? Instantiate() => ImportedMesh?.Instantiate<McolRoot>();

    public Mesh? GetMesh()
    {
        var src = (Mesh ?? ImportedMesh)?.ResourcePath;
        var root = string.IsNullOrEmpty(src) ? null : ResourceLoader.Load<PackedScene>(src, null, ResourceLoader.CacheMode.Ignore)?.Instantiate();
        if (root is McolRoot mcolRoot && mcolRoot.IsEmpty) {
            // setting this to null will force a reimport whenever the user next requests something from the resource
            ImportedResource = null;
        }
        var mesh = (root as MeshInstance3D ?? root?.FindChildByTypeRecursive<MeshInstance3D>())?.Mesh;
        root?.QueueFree();
        return mesh;
    }

    [Export] public PackedScene? Mesh { get; set; }
    [Export] public int CachedVertexCount { get; set; }

    [Export] public McolMaterialData[]? Layers;

    [ExportToolButton("Export all colliders to GLTF")]
    public Callable BtnExportFullMesh => Callable.From(ExportFullMesh);

    public override Resource? GetOrCreatePlaceholder(GodotImportOptions options)
    {
        return ImportedResource ??= CreateImporter(options).Mcol.CreateScenePlaceholder(this);
    }

    public MeshColliderResource() : base(SupportedFileFormats.MeshCollider)
    {
    }

    private void ExportFullMesh()
    {
        McolConverter.ExportToGltf(Instantiate()!, Asset!.GetImportFilepathChangeExtension(Config, "_full.gltf")!, true);
    }
}
