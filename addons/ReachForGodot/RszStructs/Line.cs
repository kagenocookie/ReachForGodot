namespace RGE;

using Godot;

public partial class Line : Resource
{
    [Export] public Vector3 from;
    [Export] public Vector3 dir;

    public static implicit operator Line(RszTool.via.Line rszValue) => new Line() {
        from = rszValue.from.ToGodot(),
        dir = rszValue.dir.ToGodot(),
    };

    public RszTool.via.Line ToRsz() => new() {
        from = from.ToRsz(),
        dir = dir.ToRsz(),
    };
}
