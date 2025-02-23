namespace RGE;

using Godot;

public partial class Ray : Resource
{
    [Export] public Vector3 from;
    [Export] public Vector3 direction;

    public static implicit operator Ray(RszTool.via.Ray rszValue) => new Ray() {
        direction = rszValue.dir.ToGodot(),
        from = rszValue.from.ToGodot(),
    };
}
