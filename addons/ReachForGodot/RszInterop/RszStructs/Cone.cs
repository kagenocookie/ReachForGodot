namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class Cone : Resource
{
    [Export] public Vector3 p0;
    [Export] public float r0;
    [Export] public Vector3 p1;
    [Export] public float r1;

    public static implicit operator Cone(ReeLib.via.Cone rszValue) => new Cone() {
        p0 = rszValue.p0.ToGodot(),
        p1 = rszValue.p1.ToGodot(),
        r0 = rszValue.r0,
        r1 = rszValue.r1
    };

    public ReeLib.via.Cone ToRsz() => new() {
        p0 = p0.ToRsz(),
        p1 = p1.ToRsz(),
        r0 = r0,
        r1 = r1,
    };
}
