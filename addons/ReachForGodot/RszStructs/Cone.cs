namespace RGE;

using Godot;

public partial class Cone : Resource
{
    [Export] public Vector3 p0;
    [Export] public float r0;
    [Export] public Vector3 p1;
    [Export] public float r1;

    public static implicit operator Cone(RszTool.via.Cone rszValue) => new Cone() {
        p0 = rszValue.p0.ToGodot(),
        p1 = rszValue.p1.ToGodot(),
        r0 = rszValue.r0,
        r1 = rszValue.r1
    };
}
