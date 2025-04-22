namespace ReaGE;

using Godot;

[GlobalClass, Tool, ResourceHolder("chf", SupportedFileFormats.ColliderHeightField)]
public partial class ColliderHeightFieldResource : REResource, IImportableAsset, IExportableAsset
{
    [Export] public HeightMapShape3D? HeightMap { get; set; }
    [Export] public Vector3 MinRange { get; set; }
    [Export] public Vector3 MaxRange { get; set; }
    [Export] public int[] PointData { get; set; } = Array.Empty<int>();
    [Export] public int[] MaskBits { get; set; } = Array.Empty<int>();
    [Export] public int[] CollisionPresetIDs { get; set; } = Array.Empty<int>();
    [Export] public string[] CollisionPresets { get; set; } = Array.Empty<string>();
    [Export] public Vector2 TileSize { get; set; }

    public ColliderHeightFieldResource() : base(SupportedFileFormats.ColliderHeightField)
    {
    }

    public bool IsEmpty => HeightMap == null;

}
