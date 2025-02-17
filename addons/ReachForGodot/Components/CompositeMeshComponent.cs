
namespace RFG;

using System;
using Godot;
using RszTool;

[GlobalClass, REComponentClass("via.render.CompositeMesh")]
public partial class CompositeMeshComponent : REComponent
{
    [Export] public Node3D? meshNode;

    public override void Setup(IRszContainerNode root, REGameObject gameObject, RszInstance rsz)
    {
        meshNode = gameObject.AddOwnedChild(new Node3D() { Name = "CompositeMesh__ComponentNode" });
        var compositeInstanceGroup = rsz.GetFieldValue("v15") as List<object>;

        if (compositeInstanceGroup != null) {
            foreach (var inst in compositeInstanceGroup.OfType<RszInstance>()) {
                if (inst.GetFieldValue("v0") is string meshFilename && meshFilename != "") {
                    var submesh = meshNode.AddOwnedChild(new MeshInstance3D() { Name = "mesh_" + meshNode.GetChildCount() });
                    if (root.Resources?.FirstOrDefault(r => r.Asset?.AssetFilename == meshFilename) is REResource mr && mr.ImportedResource is PackedScene scene) {
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
    }
}
