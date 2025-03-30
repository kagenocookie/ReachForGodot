
namespace ReaGE;

using System;
using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, Tool, REComponentClass("via.render.CompositeMesh")]
public partial class CompositeMeshComponent : REComponent, IVisualREComponent
{
    private Node3D? meshNode;
    private int childCount = 0;

    private static readonly REFieldAccessor MeshGroupsField = new REFieldAccessor("MeshGroups").Conditions("MeshGroups", "v15");

    public Node3D? GetOrFindMeshNode() => meshNode ??= GameObject.FindChildWhere<Node3D>(child => child is not ReaGE.GameObject && child.Name == "__CompositeMesh");
    private Godot.Collections.Array<REObject>? FindStoredMeshGroups()
        => TryGetFieldValue(MeshGroupsField.Get(this), out var groups) ? groups.AsGodotArray<REObject>() : null;

    public override void OnDestroy()
    {
        meshNode?.GetParent().CallDeferred(Node.MethodName.RemoveChild, meshNode);
        meshNode?.QueueFree();
        meshNode = null;
    }

    public override async Task Setup(RszImportType importType)
    {
        if (importType == RszImportType.Placeholders || importType == RszImportType.CreateOrReuse && meshNode != null) {
            return;
        }
        meshNode ??= GetOrFindMeshNode();
        childCount = 0;
        if (meshNode != null) {
            meshNode.ClearChildren();
        } else {
            meshNode = await GameObject.AddChildAsync(new Node3D() { Name = "__CompositeMesh" }, GameObject.FindRszOwnerNode());
            meshNode.LockNode(true);
        }
        var tasks = new List<Task>();
        var groups = FindStoredMeshGroups();
        if (groups != null) {
            foreach (var mg in groups) {
                if (mg.TryGetFieldValue(mg.TypeInfo.Fields[0], out var filename) && filename.AsString() is string meshFilename && !string.IsNullOrEmpty(meshFilename)) {
                    var transform = mg.GetField(mg.TypeInfo.GetFieldOrFallback("Transform", s => s.RszField.type == RszFieldType.String));
                    if (transform.VariantType == Variant.Type.Nil) {
                        GD.Print("Could not find composite mesh group transform");
                    } else {
                        tasks.Add(InstantiateSubmeshes(meshFilename, (transform.AsGodotArray<REObject>())));
                    }
                }
            }
        }
        // if ((rsz.GetFieldValue("MeshGroups") ?? rsz.GetFieldValue("v15")) is List<object> meshGroups) {
        //     foreach (var inst in meshGroups.OfType<RszInstance>()) {
        //         if (inst.Values[0] is string meshFilename && meshFilename != "") {
        //             tasks.Add(InstantiateSubmeshes(root, meshFilename, (inst.GetFieldValue("Transform") as IEnumerable<object>)?.OfType<RszInstance>()));
        //         }
        //     }
        // }
        await Task.WhenAll(tasks);
    }

    private async Task InstantiateSubmeshes(string meshFilename, IEnumerable<REObject>? transforms)
    {
        Debug.Assert(meshNode != null);
        Debug.Assert(transforms != null);

        REField? pos = null, rot = null, scale = null;

        if (Importer.FindOrImportResource<MeshResource>(meshFilename, ReachForGodot.GetAssetConfig(GameObject.Game)) is MeshResource mr) {
            var (tk, res) = await mr.Import(false).ContinueWith(static (t) => (t, t.IsCompletedSuccessfully ? t.Result : null));
            if (tk.IsCanceled) return;

            var mesh = new MultiMeshInstance3D() { Name = "mesh_" + childCount++ };
            var mm = new MultiMesh();
            mesh.Multimesh = mm;
            mm.InstanceCount = 0;
            mm.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
            if (res is PackedScene scene && scene.Instantiate<Node3D>(PackedScene.GenEditState.Instance).FindChildByTypeRecursive<MeshInstance3D>() is MeshInstance3D meshinst) {
                mm.Mesh = meshinst.Mesh;
            } else {
                mm.Mesh = new SphereMesh() { Radius = 0.5f, Height = 1, RadialSegments = 4, Rings = 2 };
            }

            mm.InstanceCount = transforms.Count();

            int i = 0;
            foreach (var tr in transforms) {
                pos ??= tr.TypeInfo.GetFieldOrFallback("Position", f => f.FieldIndex == 2);
                rot ??= tr.TypeInfo.GetFieldOrFallback("Rotation", f => f.FieldIndex == 2);
                scale ??= tr.TypeInfo.GetFieldOrFallback("Scale", f => f.FieldIndex == 2);
                mm.SetInstanceTransform(i++, RETransformComponent.Vector4x3ToTransform(
                    tr.GetField(pos).AsVector4(),
                    tr.GetField(rot).AsVector4(),
                    tr.GetField(scale).AsVector4()
                    // (System.Numerics.Vector4)tr.Values[2],
                    // (System.Numerics.Vector4)tr.Values[3],
                    // (System.Numerics.Vector4)tr.Values[4]
                ));
            }

            await meshNode.AddChildAsync(mesh, GameObject.Owner);
        }
    }

    public Aabb GetBounds()
    {
        var aabb = new Aabb();
        var meshnode = GetOrFindMeshNode();
        var groups = FindStoredMeshGroups();
        if (groups != null) {
            foreach (var mg in groups) {
                var transforms = mg.GetField(mg.TypeInfo.GetFieldOrFallback("Transform", s => s.RszField.type == RszFieldType.String)).AsGodotArray<REObject>();
                foreach (var tr in transforms) {
                    var posfield = tr.TypeInfo.GetFieldOrFallback("Position", f => f.FieldIndex == 2);
                    var origin = tr.GetField(posfield).AsVector4().ToVector3();
                    aabb = aabb.Position.IsZeroApprox() && aabb.Size.IsZeroApprox() ? new Aabb(origin, Vector3.Zero) : aabb.Expand(origin);
                }
            }
        }
        return aabb;
    }
}
