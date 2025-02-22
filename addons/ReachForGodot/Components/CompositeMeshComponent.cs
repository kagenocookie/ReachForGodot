
namespace RGE;

using System;
using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, REComponentClass("via.render.CompositeMesh")]
public partial class CompositeMeshComponent : REComponent
{
    [Export] public Node3D? meshNode;
    private int childCount = 0;

    public override void _ExitTree()
    {
        meshNode?.GetParent().CallDeferred(Node.MethodName.RemoveChild, meshNode);
        meshNode?.QueueFree();
        meshNode = null;
    }

    public override async Task Setup(IRszContainerNode root, REGameObject gameObject, RszInstance rsz, RszImportType importType)
    {
        if (importType == RszImportType.Placeholders || importType == RszImportType.Import && meshNode != null) {
            return;
        }
        childCount = 0;
        if (meshNode != null) {
            meshNode.ClearChildren();
        } else {
            meshNode = await gameObject.AddChildAsync(new Node3D() { Name = "__CompositeMesh" }, root as Node);
        }
        var tasks = new List<Task>();
        if ((rsz.GetFieldValue("MeshGroups") ?? rsz.GetFieldValue("v15")) is List<object> meshGroups) {
            foreach (var inst in meshGroups.OfType<RszInstance>()) {
                if (inst.Values[0] is string meshFilename && meshFilename != "") {
                    tasks.Add(InstantiateSubmeshes(root, meshFilename, (inst.GetFieldValue("Transform") as IEnumerable<object>)?.OfType<RszInstance>()));
                }
            }
        }
        await Task.WhenAll(tasks);
    }

    private async Task InstantiateSubmeshes(IRszContainerNode root, string meshFilename, IEnumerable<RszInstance>? transforms)
    {
        Debug.Assert(meshNode != null);
        Debug.Assert(transforms != null);

        if (root.Resources?.FirstOrDefault(r => r.Asset?.IsSameAsset(meshFilename) == true) is MeshResource mr) {
            var res = await mr.Import(false).ContinueWith(static (t) => t.IsFaulted ? null : t.Result);

            foreach (var tr in transforms) {
                var reobj = new REObject(root.Game, tr.RszClass.name, tr);
                var submesh = res is PackedScene scene ? scene.Instantiate<Node3D>(PackedScene.GenEditState.Instance) : new MeshInstance3D() { };
                submesh.Name = "mesh_" + childCount++;
                meshNode.AddDeferredChild(submesh, root as Node);
                SphereMesh? newMesh = null;
                if (res == null && submesh is MeshInstance3D mi) {
                    mi.SetDeferred("mesh", newMesh = new SphereMesh() { Radius = 0.5f, Height = 1 });
                }
                submesh.Transform = RETransformComponent.Vector4x3ToTransform(
                    reobj._Get("Position").AsVector4(),
                    reobj._Get("Rotation").AsVector4(),
                    reobj._Get("Scale").AsVector4());

                while (submesh.GetParent() != meshNode) {
                    await Task.Delay(1);
                }
            }
        }
    }
}
