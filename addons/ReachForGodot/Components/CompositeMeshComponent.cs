
namespace RGE;

using System;
using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, REComponentClass("via.render.CompositeMesh")]
public partial class CompositeMeshComponent : REComponent
{
    public Node3D? meshNode;
    private int childCount = 0;

    public override void OnDestroy()
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

            var mesh = new MultiMeshInstance3D() { Name = "mesh_" + childCount++ };
            var mm = new MultiMesh();
            mesh.Multimesh = mm;
            mm.InstanceCount = 0;
            if (res is PackedScene scene && scene.Instantiate<Node3D>(PackedScene.GenEditState.Instance).FindChildByTypeRecursive<MeshInstance3D>() is MeshInstance3D meshinst) {
                mm.Mesh = meshinst.Mesh;
            } else {
                mm.Mesh = new SphereMesh() { Radius = 0.5f, Height = 1 };
            }
            mm.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;

            mm.InstanceCount = transforms.Count();

            int i = 0;
            foreach (var tr in transforms) {
                mm.SetInstanceTransform(i++, RETransformComponent.Vector4x3ToTransform(
                    (System.Numerics.Vector4)tr.Values[2],
                    (System.Numerics.Vector4)tr.Values[3],
                    (System.Numerics.Vector4)tr.Values[4]
                ));
            }

            await meshNode.AddChildAsync(mesh, root as Node);
        }
    }
}
