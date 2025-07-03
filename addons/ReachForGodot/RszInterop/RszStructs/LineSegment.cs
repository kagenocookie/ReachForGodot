namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class LineSegment : Resource
{
    [Export] public Vector3 start;
    [Export] public Vector3 end;

    public static implicit operator LineSegment(ReeLib.via.LineSegment rszValue) => new LineSegment() {
        start = rszValue.start.ToGodot(),
        end = rszValue.end.ToGodot(),
    };

    public ReeLib.via.LineSegment ToRsz() => new() {
        start = start.ToRsz(),
        end = end.ToRsz(),
    };
}
