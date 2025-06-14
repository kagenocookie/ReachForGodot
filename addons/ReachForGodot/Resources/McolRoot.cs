namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class McolRoot : Node, IExportableAsset, IImportableAsset
{
    [Export] public SupportedGame Game { get; set; }
    [Export] public AssetReference? Asset { get; set; }

    public MeshColliderResource? Resource => Importer.FindOrImportResource<MeshColliderResource>(Asset, ReachForGodot.GetAssetConfig(Game), !string.IsNullOrEmpty(SceneFilePath));

    public bool IsEmpty => this.GetChildCount() == 0;

    [ExportToolButton("Export all colliders to GLTF")]
    public Callable BtnExportFullMesh => Callable.From(ExportFullMesh);

    public Node? MeshContainerNode {
        get {
            return GetNodeOrNull<Node3D>("Mesh");
        }
    }

    public MeshInstance3D? MeshNode {
        get {
            var meshnode = MeshContainerNode;
            return meshnode as MeshInstance3D ?? meshnode?.FindChildByTypeRecursive<MeshInstance3D>();
        }
    }

    public StaticBody3D ColliderRoot {
        get {
            var meshnode = GetNodeOrNull<StaticBody3D>("Colliders");
            if (meshnode == null) {
                AddChild(meshnode = new StaticBody3D() { Name = "Colliders" });
                meshnode.Owner = Owner ?? this;
            }
            return meshnode;
        }
    }

    private void ExportFullMesh()
    {
        McolConverter.ExportToGltf(this, Asset!.GetImportFilepathChangeExtension(ReachForGodot.GetAssetConfig(Game), "_full.gltf")!, true);
    }
}
