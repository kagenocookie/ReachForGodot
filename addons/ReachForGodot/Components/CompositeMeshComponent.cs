
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

    private static readonly REFieldAccessor InstanceGroups = new REFieldAccessor("InstanceGroups").NameOrConditions(f => f.FirstOrDefault(x => x.RszField.array));

    [REObjectFieldTarget("via.render.CompositeMeshInstanceGroup")]
    private static readonly REFieldAccessor GroupMesh = new REFieldAccessor("Mesh").NameOrConditions(f => f.FirstOrDefault(x => x.RszField.type is RszFieldType.Resource));

    [REObjectFieldTarget("via.render.CompositeMeshInstanceGroup")]
    private static readonly REFieldAccessor GroupMaterial = new REFieldAccessor("Material").NameOrConditions(f => f.Where(x => x.RszField.type is RszFieldType.Resource).Skip(1).FirstOrDefault());

    /// <summary>
    /// via.render.CompositeMeshTransformController[]
    /// </summary>
    [REObjectFieldTarget("via.render.CompositeMeshInstanceGroup")]
    private static readonly REFieldAccessor GroupTransforms = new REFieldAccessor("Transforms").NameOrConditions(f => f.FirstOrDefault(x => x.RszField.array));

    [REObjectFieldTarget("via.render.CompositeMeshTransformController")]
    private static readonly REFieldAccessor ControllerPosition = new REFieldAccessor("Position").NameOrConditions(f => f.Where(x => x.RszField.type is RszFieldType.Vec4).FirstOrDefault());

    [REObjectFieldTarget("via.render.CompositeMeshTransformController")]
    private static readonly REFieldAccessor ControllerRotation = new REFieldAccessor("Rotation").NameOrConditions(f => f.Where(x => x.RszField.type is RszFieldType.Vec4).Skip(1).FirstOrDefault());

    [REObjectFieldTarget("via.render.CompositeMeshTransformController")]
    private static readonly REFieldAccessor ControllerScale = new REFieldAccessor("Scale").NameOrConditions(f => f.Where(x => x.RszField.type is RszFieldType.Vec4).Skip(2).FirstOrDefault());

    public Node3D? GetOrFindMeshNode() => meshNode ??= GameObject.FindChildWhere<Node3D>(child => child is not ReaGE.GameObject && child.Name == "__CompositeMesh");
    private Godot.Collections.Array<REObject>? FindStoredMeshGroups()
        => TryGetFieldValue(InstanceGroups.Get(this), out var groups) ? groups.AsGodotArray<REObject>() : null;

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
                var mesh = mg.GetField(GroupMesh).As<MeshResource>();
                // var mat = mg.GetField(GroupMaterial).As<MaterialDefinitionResource>();
                var transforms = mg.GetField(GroupTransforms).AsGodotArray<REObject>();
                if (mesh != null && transforms != null) {
                    tasks.Add(InstantiateSubmeshes(mesh, transforms));
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

    private async Task InstantiateSubmeshes(MeshResource? mr, IEnumerable<REObject>? transforms)
    {
        Debug.Assert(meshNode != null);
        Debug.Assert(transforms != null);

        if (mr != null) {
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
                mm.SetInstanceTransform(i++, RETransformComponent.Vector4x3ToTransform(
                    tr.GetField(ControllerPosition).AsVector4(),
                    tr.GetField(ControllerRotation).AsVector4(),
                    tr.GetField(ControllerScale).AsVector4()
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
                var transforms = mg.GetField(GroupTransforms).AsGodotArray<REObject>();
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
