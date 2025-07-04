namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class Torus : Resource
{
    [Export] public Vector3 pos;
    [Export] public float r;
    [Export] public Vector3 axis;
    [Export] public float cr;

    public static implicit operator Torus(ReeLib.via.Torus rszValue) => new Torus() {
        pos = rszValue.pos.ToGodot(),
        r = rszValue.r,
        axis = rszValue.axis.ToGodot(),
        cr = rszValue.cr,
    };

    public ReeLib.via.Torus ToRsz() => new() {
        pos = pos.ToRsz(),
        axis = axis.ToRsz(),
        r = r,
        cr = cr,
    };
}
