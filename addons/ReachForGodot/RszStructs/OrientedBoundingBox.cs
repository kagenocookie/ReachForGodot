namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class OrientedBoundingBox : Resource
{
    [Export] public Projection coord;
    [Export] public Vector3 extent;

    public static OrientedBoundingBox FromRsz(RszTool.via.OBB rszValue, SupportedGame game) => new OrientedBoundingBox() {
        coord = rszValue.Coord.ToProjection(game),
        extent = rszValue.Extent.ToGodot(),
    };

    public RszTool.via.OBB ToRsz(SupportedGame game) => new() {
        Coord = coord.ToRsz(game),
        Extent = extent.ToRsz(),
    };
}
