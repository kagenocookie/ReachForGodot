namespace RGE;

using Godot;

public partial class LineSegment : Resource
{
    [Export] public Vector3 start;
    [Export] public Vector3 end;

    public static implicit operator LineSegment(RszTool.via.LineSegment rszValue) => new LineSegment() {
        start = rszValue.start.ToGodot(),
        end = rszValue.end.ToGodot(),
    };

    public RszTool.via.LineSegment ToRsz() => new() {
        start = start.ToRsz(),
        end = end.ToRsz(),
    };
}
