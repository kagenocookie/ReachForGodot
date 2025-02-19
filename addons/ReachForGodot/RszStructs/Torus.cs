namespace RGE;

using System;
using Godot;

public partial class Torus : Resource
{
    [Export] public Vector3 pos;
    [Export] public float r;
    [Export] public Vector3 axis;
    [Export] public float cr;

    public static implicit operator Torus(RszTool.via.Torus rszValue) => new Torus() {
        pos = rszValue.pos.ToGodot(),
        r = rszValue.r,
        axis = rszValue.axis.ToGodot(),
        cr = rszValue.cr,
    };
}
