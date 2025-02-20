
namespace RGE;

using System;
using Godot;
using RszTool;

[GlobalClass, REComponentClass("via.render.CompositeMesh")]
public partial class CompositeMeshComponent : REComponent
{
    [Export] public Node3D? meshNode;
    private int childCount = 0;

    public override void Setup(IRszContainerNode root, REGameObject gameObject, RszInstance rsz)
    {
        childCount = 0;
        meshNode = gameObject.AddDeferredChild(new Node3D() { Name = "__CompositeMesh" });
        if ((rsz.GetFieldValue("MeshGroups") ?? rsz.GetFieldValue("v15")) is List<object> meshGroups) {
            foreach (var inst in meshGroups.OfType<RszInstance>()) {
                if (inst.Values[0] is string meshFilename && meshFilename != "") {
                    InstantiateSubmeshes(root, meshFilename, (inst.GetFieldValue("Transforms") as IEnumerable<object>)?.OfType<RszInstance>());
                }
            }
        }
    }

    private void InstantiateSubmeshes(IRszContainerNode root, string meshFilename, IEnumerable<RszInstance>? transforms)
    {
        Debug.Assert(meshNode != null);
        Debug.Assert(transforms != null);

        if (root.Resources?.FirstOrDefault(r => r.Asset?.IsSameAsset(meshFilename) == true) is MeshResource mr) {
            mr.Import(false).ContinueWith((res) => {
                if (res.Result is PackedScene scene) {
                    foreach (var tr in transforms) {
                        var reobj = new REObject(root.Game, tr.RszClass.name, tr);
                        var child = scene.Instantiate<Node3D>(PackedScene.GenEditState.Instance);
                        child.Name = "mesh_" + childCount++;
                        meshNode.AddDeferredChild(child);
                        child.Transform = RETransformComponent.Vector4x3ToTransform(
                            reobj._Get("Position").AsVector4(),
                            reobj._Get("Rotation").AsVector4(),
                            reobj._Get("Scale").AsVector4());
                    }
                } else {
                    // add placeholder nodes so we at least get a sense of where stuff is at
                    foreach (var tr in transforms) {
                        var reobj = new REObject(root.Game, tr.RszClass.name, tr);
                        var submesh = meshNode.AddDeferredChild(new MeshInstance3D() { Name = "mesh_" + childCount++ });
                        submesh.Transform = RETransformComponent.Vector4x3ToTransform(
                            reobj._Get("Position").AsVector4(),
                            reobj._Get("Rotation").AsVector4(),
                            reobj._Get("Scale").AsVector4());
                        submesh.SetDeferred("mesh", new SphereMesh() { Radius = 0.5f, Height = 1 });
                    }
                }
            });
        }
    }
}
