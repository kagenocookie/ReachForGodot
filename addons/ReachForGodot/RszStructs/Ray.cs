namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class Ray : Resource
{
    [Export] public Vector3 from;
    [Export] public Vector3 direction;

    public static implicit operator Ray(RszTool.via.Ray rszValue) => new Ray() {
        direction = rszValue.dir.ToGodot(),
        from = rszValue.from.ToGodot(),
    };

    public RszTool.via.Ray ToRsz() => new() {
        from = from.ToRsz(),
        dir = direction.ToRsz(),
    };
}
