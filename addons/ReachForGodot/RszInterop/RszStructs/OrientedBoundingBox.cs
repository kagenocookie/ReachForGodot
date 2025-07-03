namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class OrientedBoundingBox : Resource
{
    [Export] public Projection coord;
    [Export] public Vector3 extent;

    public static OrientedBoundingBox FromRsz(ReeLib.via.OBB rszValue) => new OrientedBoundingBox() {
        coord = rszValue.Coord.ToProjection(),
        extent = rszValue.Extent.ToGodot(),
    };

    public ReeLib.via.OBB ToRsz() => new() {
        Coord = coord.ToRsz(),
        Extent = extent.ToRsz(),
    };
}
