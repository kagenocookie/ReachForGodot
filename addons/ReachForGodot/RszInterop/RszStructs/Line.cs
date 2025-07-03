namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class Line : Resource
{
    [Export] public Vector3 from;
    [Export] public Vector3 dir;

    public static implicit operator Line(ReeLib.via.Line rszValue) => new Line() {
        from = rszValue.from.ToGodot(),
        dir = rszValue.dir.ToGodot(),
    };

    public ReeLib.via.Line ToRsz() => new() {
        from = from.ToRsz(),
        dir = dir.ToRsz(),
    };
}
