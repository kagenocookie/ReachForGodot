namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class Ellipsoid : Resource
{
    [Export] public Vector3 pos;
    [Export] public Vector3 r;

    public static implicit operator Ellipsoid(ReeLib.via.Ellipsoid rszValue) => new Ellipsoid() {
        pos = rszValue.pos.ToGodot(),
        r = rszValue.r.ToGodot(),
    };

    public ReeLib.via.Ellipsoid ToRsz() => new() {
        pos = pos.ToRsz(),
        r = r.ToRsz(),
    };
}
