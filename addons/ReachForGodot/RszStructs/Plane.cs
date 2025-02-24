namespace RGE;

using Godot;

[GlobalClass, Tool]
public partial class Plane : Resource
{
    [Export] public Vector3 normal;
    [Export] public float dist;

    public static implicit operator Plane(RszTool.via.Plane rszValue) => new Plane() {
        normal = rszValue.normal.ToGodot(),
        dist = rszValue.dist,
    };

    public RszTool.via.Plane ToRsz() => new() {
        normal = normal.ToRsz(),
        dist = dist,
    };
}
