namespace RFG;

using System;
using Godot;
using RszTool;

public static class ComponentTypes
{
    public static void Init()
    {
        GodotScnConverter.DefineComponentFactory("via.render.Mesh", SetupMesh);
        GodotScnConverter.DefineComponentFactory("via.Transform", SetupTransform);
    }

    private static Node? SetupMesh(ScnFile scn, REGameObject gameObject, RszInstance rsz)
    {
        var node = gameObject.AddOwnedChild(new MeshInstance3D() { Name = "via.render.Mesh" });
        var meshPath = (rsz.GetFieldValue("v2") as string);
        var mdfPath = (rsz.GetFieldValue("v3") as string);

        return node;
    }

    private static Node? SetupTransform(ScnFile scn, REGameObject gameObject, RszInstance rsz)
    {
        if (gameObject.Root3D != null) {
            var row1 = (System.Numerics.Vector4)rsz.GetFieldValue("v0")!;
            var row2 = (System.Numerics.Vector4)rsz.GetFieldValue("v1")!;
            var row3 = (System.Numerics.Vector4)rsz.GetFieldValue("v2")!;
            gameObject.Root3D.Transform = new Transform3D(
                new Vector3(row1.X, row2.X, row3.X),
                new Vector3(row1.Y, row2.Y, row3.Y),
                new Vector3(row1.Z, row2.Z, row3.Z),
                new Vector3(row1.W, row2.W, row3.W)
            );
        }
        return null;
    }
}