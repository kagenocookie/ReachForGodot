namespace RGE;

using System;
using Godot;

public partial class Rect3D : Resource
{
    [Export] public Vector3 normal;
    [Export] public float sizeW;
    [Export] public Vector3 center;
    [Export] public float sizeH;

    public static implicit operator Rect3D(RszTool.via.Rect3D rszValue) => new Rect3D() {
        normal = rszValue.normal.ToGodot(),
        center = rszValue.center.ToGodot(),
        sizeW = rszValue.sizeW,
        sizeH = rszValue.sizeH,
    };
}
