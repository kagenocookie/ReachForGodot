namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class Ellipsoid : Resource
{
    [Export] public Vector3 pos;
    [Export] public Vector3 r;

    public static implicit operator Ellipsoid(RszTool.via.Ellipsoid rszValue) => new Ellipsoid() {
        pos = rszValue.pos.ToGodot(),
        r = rszValue.r.ToGodot(),
    };

    public RszTool.via.Ellipsoid ToRsz() => new() {
        pos = pos.ToRsz(),
        r = r.ToRsz(),
    };
}
