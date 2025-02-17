namespace RFG;

using System;
using Godot;
using RszTool;

[GlobalClass, REComponentClass("via.Transform")]
public partial class RETransformComponent : REComponent
{
    public override void Setup(IRszContainerNode root, REGameObject gameObject, RszInstance rsz)
    {
        var row1 = (System.Numerics.Vector4)rsz.Values[0];
        var row2 = (System.Numerics.Vector4)rsz.Values[1];
        var row3 = (System.Numerics.Vector4)rsz.Values[2];
        var scale = new Vector3(row3.X, row3.Y, row3.Z);
        gameObject.Transform = new Transform3D(
            new Basis(new Quaternion(row2.X, row2.Y, row2.Z, row2.W)).Scaled(scale),
            new Vector3(row1.X, row1.Y, row1.Z)
        );
    }
}