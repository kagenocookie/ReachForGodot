namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class Segment : Resource
{
    [Export] public Vector4 from;
    [Export] public Vector3 dir;

    public static implicit operator Segment(ReeLib.via.Segment rszValue) => new Segment() {
        from = rszValue.from.ToGodot(),
        dir = rszValue.dir.ToGodot(),
    };

    public ReeLib.via.Segment ToRsz() => new() {
        from = from.ToRsz(),
        dir = dir.ToRsz(),
    };
}
