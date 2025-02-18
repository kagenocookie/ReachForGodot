namespace RGE;

using System;
using Godot;

public partial class OrientedBoundingBox : Resource
{
    [Export] public Projection coord;
    [Export] public Vector3 extent;

    public OrientedBoundingBox()
    {
    }

    public OrientedBoundingBox(RszTool.via.OBB obb)
    {
        coord = obb.Coord.ToProjection();
        extent = obb.Extent.ToGodot();
    }
}
