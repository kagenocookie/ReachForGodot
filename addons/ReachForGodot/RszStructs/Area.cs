namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class Area : Resource
{
    [Export] public Vector2 p0;
    [Export] public Vector2 p1;
    [Export] public Vector2 p2;
    [Export] public Vector2 p3;
    [Export] public float height;
    [Export] public float bottom;

    public static implicit operator Area(RszTool.via.Area rszValue) => new Area() {
        p0 = rszValue.p0.ToGodot(),
        p1 = rszValue.p1.ToGodot(),
        p2 = rszValue.p2.ToGodot(),
        p3 = rszValue.p3.ToGodot(),
        height = rszValue.height,
        bottom = rszValue.bottom,
    };

    public RszTool.via.Area ToRsz() => new() {
        p0 = p0.ToRsz(),
        p1 = p1.ToRsz(),
        p2 = p2.ToRsz(),
        p3 = p3.ToRsz(),
        height = height,
        bottom = bottom,
    };
}
