namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class RayY : Resource
{
    [Export] public Vector3 from;
    [Export] public float dir;

    public static implicit operator RayY(ReeLib.via.RayY rszValue) => new RayY() {
        from = rszValue.from.ToGodot(),
        dir = rszValue.dir,
    };

    public ReeLib.via.RayY ToRsz() => new() {
        from = from.ToRsz(),
        dir = dir,
    };
}
