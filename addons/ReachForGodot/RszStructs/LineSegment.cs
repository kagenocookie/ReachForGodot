namespace RGE;

using Godot;

public partial class LineSegment : Resource
{
    [Export] public Vector3 pos;
    [Export] public Vector3 end;

    public static implicit operator LineSegment(RszTool.via.LineSegment rszValue) => new LineSegment() {
        pos = rszValue.start.ToGodot(),
        end = rszValue.end.ToGodot(),
    };
}
