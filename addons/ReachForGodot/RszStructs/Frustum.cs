namespace RGE;

using Godot;

public partial class Frustum : Resource
{
    [Export] public Plane? plane0;
    [Export] public Plane? plane1;
    [Export] public Plane? plane2;
    [Export] public Plane? plane3;
    [Export] public Plane? plane4;
    [Export] public Plane? plane5;

    public static implicit operator Frustum(RszTool.via.Frustum rszValue) => new Frustum() {
        plane0 = (Plane)rszValue.plane0,
        plane1 = (Plane)rszValue.plane1,
        plane2 = (Plane)rszValue.plane2,
        plane3 = (Plane)rszValue.plane3,
        plane4 = (Plane)rszValue.plane4,
        plane5 = (Plane)rszValue.plane5,
    };

    public RszTool.via.Frustum ToRsz() => new() {
        plane0 = plane0?.ToRsz() ?? default,
        plane1 = plane1?.ToRsz() ?? default,
        plane2 = plane2?.ToRsz() ?? default,
        plane3 = plane3?.ToRsz() ?? default,
        plane4 = plane4?.ToRsz() ?? default,
        plane5 = plane5?.ToRsz() ?? default,
    };
}
