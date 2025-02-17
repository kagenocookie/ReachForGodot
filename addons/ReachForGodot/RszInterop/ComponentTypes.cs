namespace RFG;

using System;
using Godot;
using RszTool;

public static class ComponentTypes
{
    public static void Init()
    {
        RszGodotConverter.DefineComponentFactory("via.render.Mesh", SetupMesh);
        RszGodotConverter.DefineComponentFactory("via.render.CompositeMesh", SetupCompositeMesh);
        RszGodotConverter.DefineComponentFactory("via.Transform", SetupTransform);
    }

    private static Node? SetupMesh(IRszContainerNode root, REGameObject gameObject, RszInstance rsz)
    {
        var meshPath = rsz.GetFieldValue("v2") as string ?? rsz.GetFieldValue("v20") as string ?? rsz.Values.FirstOrDefault(v => v is string) as string;

        Node3D node;
        if (root.Resources?.FirstOrDefault(r => r.SourcePath == meshPath) is REResource mr && mr.ImportedResource is PackedScene scene) {
            node = scene.Instantiate<Node3D>(PackedScene.GenEditState.Instance);
            if (node == null) {
                GD.PrintErr("Invalid mesh source scene " + mr.ImportedPath);
                return gameObject.AddOwnedChild(node = new Node3D() { Name = rsz.RszClass.name });
            }
            gameObject.AddOwnedChild(node);
        } else {
            GD.Print("Missing mesh " + meshPath + " at path: " + gameObject.Owner.GetPathTo(gameObject));
            gameObject.AddOwnedChild(node = new Node3D() { Name = rsz.RszClass.name });
        }
        return node;
    }

    private static Node? SetupCompositeMesh(IRszContainerNode root, REGameObject gameObject, RszInstance rsz)
    {
        var node = gameObject.AddOwnedChild(new Node3D() { Name = "via.render.CompositeMesh" });
        var compositeInstanceGroup = rsz.GetFieldValue("v15") as List<object>;

        if (compositeInstanceGroup != null) {
            foreach (var inst in compositeInstanceGroup.OfType<RszInstance>()) {
                if (inst.GetFieldValue("v0") is string meshFilename && meshFilename != "") {
                    var submesh = node.AddOwnedChild(new MeshInstance3D() { Name = "mesh_" + node.GetChildCount() });
                    if (root.Resources?.FirstOrDefault(r => r.SourcePath == meshFilename) is REResource mr && mr.ImportedResource is PackedScene scene) {
                        var sourceMeshInstance = scene.Instantiate()?.FindChildByType<MeshInstance3D>();
                        if (sourceMeshInstance != null) {
                            submesh.Mesh = sourceMeshInstance.Mesh;
                        }
                    }

                    if (submesh.Mesh == null) {
                        submesh.Mesh = new BoxMesh();
                    }
                }
            }
        }
        return node;
    }

    private static Node? SetupTransform(IRszContainerNode root, REGameObject gameObject, RszInstance rsz)
    {
        var row1 = (System.Numerics.Vector4)rsz.Values[0];
        var row2 = (System.Numerics.Vector4)rsz.Values[1];
        var row3 = (System.Numerics.Vector4)rsz.Values[2];
        var scale = new Vector3(row3.X, row3.Y, row3.Z);
        gameObject.Transform = new Transform3D(
            new Basis(new Quaternion(row2.X, row2.Y, row2.Z, row2.W)).Scaled(scale),
            new Vector3(row1.X, row1.Y, row1.Z)
        );
        return null;
    }
}