namespace RGE;

using Godot;

public partial class Capsule : Resource
{
    [Export] public Vector3 p0;
    [Export] public Vector3 p1;
    [Export] public float r;

    public static implicit operator Capsule(RszTool.via.Capsule rszValue) => new Capsule() {
        p0 = rszValue.p0.ToGodot(),
        p1 = rszValue.p1.ToGodot(),
        r = rszValue.r,
    };

    public RszTool.via.Capsule ToRsz() => new() {
        p0 = p0.ToRsz(),
        p1 = p1.ToRsz(),
        r = r,
    };
}
