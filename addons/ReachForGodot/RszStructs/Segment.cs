namespace RGE;

using Godot;

public partial class Segment : Resource
{
    [Export] public Vector4 from;
    [Export] public Vector3 dir;

    public static implicit operator Segment(RszTool.via.Segment rszValue) => new Segment() {
        from = rszValue.from.ToGodot(),
        dir = rszValue.dir.ToGodot(),
    };
}
