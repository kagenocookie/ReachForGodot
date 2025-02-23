namespace RGE;

using Godot;

public partial class Triangle : Resource
{
    [Export] public Vector3 p0;
    [Export] public Vector3 p1;
    [Export] public Vector3 p2;

    public static implicit operator Triangle(RszTool.via.Triangle rszValue) => new Triangle() {
        p0 = rszValue.p0.ToGodot(),
        p1 = rszValue.p1.ToGodot(),
        p2 = rszValue.p2.ToGodot(),
    };
}
