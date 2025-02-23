namespace RGE;

using Godot;

public partial class Ellipsoid : Resource
{
    [Export] public Vector3 pos;
    [Export] public Vector3 r;

    public static implicit operator Ellipsoid(RszTool.via.Ellipsoid rszValue) => new Ellipsoid() {
        pos = rszValue.pos.ToGodot(),
        r = rszValue.r.ToGodot(),
    };
}
