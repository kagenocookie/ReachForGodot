namespace RGE;

using System;
using Godot;
using RszTool;

[GlobalClass, Tool, REComponentClass("via.Transform")]
public partial class RETransformComponent : REComponent
{
    public override void Setup(IRszContainerNode root, REGameObject gameObject, RszInstance rsz)
    {
        gameObject.Transform = Vector4x3ToTransform(
            (System.Numerics.Vector4)rsz.Values[0],
            (System.Numerics.Vector4)rsz.Values[1],
            (System.Numerics.Vector4)rsz.Values[2]
        );
    }

    public static Transform3D Vector4x3ToTransform(Vector4 pos, Vector4 rotation, Vector4 scale) => new Transform3D(
        new Basis(rotation.ToQuaternion()).Scaled(scale.ToVector3()),
        pos.ToVector3()
    );

    public static Transform3D Vector4x3ToTransform(System.Numerics.Vector4 pos, System.Numerics.Vector4 rotation, System.Numerics.Vector4 scale)
    {
        var row1 = (System.Numerics.Vector4)pos;
        var row2 = (System.Numerics.Vector4)rotation;
        var row3 = (System.Numerics.Vector4)scale;
        return new Transform3D(
            new Basis(new Quaternion(row2.X, row2.Y, row2.Z, row2.W))
                .Scaled(new Vector3(row3.X, row3.Y, row3.Z)),
            new Vector3(row1.X, row1.Y, row1.Z)
        );
    }
}