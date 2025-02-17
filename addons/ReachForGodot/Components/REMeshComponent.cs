namespace RFG;

using System;
using Godot;
using RszTool;

[GlobalClass, REComponentClass("via.render.Mesh")]
public partial class REMeshComponent : REComponent
{
    [Export] public Node3D? meshNode;

    public override void Setup(IRszContainerNode root, REGameObject gameObject, RszInstance rsz)
    {
        var meshPath = rsz.GetFieldValue("v2") as string ?? rsz.GetFieldValue("v20") as string ?? rsz.Values.FirstOrDefault(v => v is string) as string;

        if (root.Resources?.FirstOrDefault(r => r.SourcePath == meshPath) is REResource mr && mr.ImportedResource is PackedScene scene) {
            meshNode = scene.Instantiate<Node3D>(PackedScene.GenEditState.Instance);
            if (meshNode == null) {
                GD.PrintErr("Invalid mesh source scene " + mr.ImportedPath);
                return;
            }
            meshNode.Name += "__ComponentNode";
            gameObject.AddOwnedChild(meshNode);
        } else {
            meshNode = null;
            GD.Print("Missing mesh " + meshPath + " at path: " + gameObject.Owner.GetPathTo(gameObject));
        }
    }
}