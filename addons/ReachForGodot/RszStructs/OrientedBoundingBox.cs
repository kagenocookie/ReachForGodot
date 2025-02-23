namespace RGE;

using Godot;

public partial class OrientedBoundingBox : Resource
{
    [Export] public Projection coord;
    [Export] public Vector3 extent;

    public static implicit operator OrientedBoundingBox(RszTool.via.OBB rszValue) => new OrientedBoundingBox() {
        coord = rszValue.Coord.ToProjection(),
        extent = rszValue.Extent.ToGodot(),
    };
}
