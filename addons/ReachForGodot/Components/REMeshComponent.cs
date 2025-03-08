namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using JetBrains.Annotations;
using RszTool;

[GlobalClass, Tool, REComponentClass("via.render.Mesh")]
public partial class REMeshComponent : REComponent, IVisualREComponent
{
    private static readonly REObjectFieldAccessor MeshField = new REObjectFieldAccessor(
        "Mesh",
        (fields) => fields.FirstOrDefault(f => f.RszField.type is RszFieldType.String or RszFieldType.Resource),
        (field) => field.ResourceType = RESupportedFileFormats.Mesh);

    private static readonly REObjectFieldAccessor MaterialField = new REObjectFieldAccessor(
        "Material",
        (fields) => fields.Where(f => f.RszField.type is RszFieldType.String or RszFieldType.Resource).Skip(1).FirstOrDefault(),
        (field) => field.ResourceType = RESupportedFileFormats.Material);

    private Node3D? meshNode;
    public MeshResource? Resource => TryGetFieldValue(MeshField.Get(this), out var path) ? path.As<MeshResource>() : null;

    [ExportToolButton("Reinstantiate mesh")]
    private Callable ForceReinstance => Callable.From(FindResourceAndReinit);

    public Node3D? GetOrFindMeshNode()
    {
        if (meshNode != null && !IsInstanceValid(meshNode)) {
            meshNode = null;
        }
        meshNode ??= GameObject.FindChildWhere<Node3D>(child => child.GetType() == typeof(Node3D) && child.Name.ToString().StartsWith("__"));
        if (!IsInstanceValid(meshNode)) {
            meshNode = null;
        }
        return meshNode;
    }

    private void FindResourceAndReinit()
    {
        _ = ReloadMesh(Resource, true);
    }

    public override void OnDestroy()
    {
        if (meshNode != null) {
            if (!meshNode.IsQueuedForDeletion()) {
                meshNode.GetParent().CallDeferred(Node.MethodName.RemoveChild, meshNode);
                meshNode.QueueFree();
            }
            meshNode = null;
        }
    }

    private bool IsCorrectMesh(MeshResource mr)
    {
        return Resource?.ResourcePath == mr.ResourcePath;
    }

    public override bool _Set(StringName property, Variant value)
    {
        var r = base._Set(property, value);
        if (MeshField.IsMatch(this, property)) {
            var resource = value.As<MeshResource>();
            ReinstantiateMesh(resource?.ImportedResource as PackedScene);
            if (resource != null) EnsureResourceInContainer(resource);
        }
        if (MaterialField.IsMatch(this, property)) {
            EnsureResourceInContainer(value.As<MaterialResource>());
        }
        return r;
    }

    public override async Task Setup(REGameObject gameObject, RszInstance rsz, RszImportType importType)
    {
        GameObject = gameObject;
        meshNode ??= GetOrFindMeshNode();
        if (Resource == null) {
            meshNode?.QueueFree();
            meshNode = null;
            return;
        }

        if (importType == RszImportType.Placeholders || importType == RszImportType.CreateOrReuse && meshNode != null) {
            return;
        }

        await ReloadMesh(Resource, importType == RszImportType.ForceReimport);
    }

    protected async Task ReloadMesh(MeshResource? mr, bool forceReload)
    {
        if (mr != null) {
            var (tk, res) = await mr.Import(forceReload).ContinueWith(static (t) => (t, t.IsCompletedSuccessfully ? t.Result : null));
            if (tk.IsCanceled) return;
            await ReinstantiateMesh(res as PackedScene);
        } else {
            meshNode?.QueueFree();
            meshNode = null;
        }
    }

    public Task ReinstantiateMesh(PackedScene? scene)
    {
        meshNode = GetOrFindMeshNode();
        meshNode?.Free();
        if (scene != null) {
            meshNode = scene.Instantiate<Node3D>(PackedScene.GenEditState.Instance);
            meshNode.Name = "__" + meshNode.Name;
        } else {
            var mi = new MeshInstance3D() { Name = "__Mesh" };
            meshNode = mi;
            mi.Mesh = new SphereMesh() { Radius = 0.5f, Height = 1, RadialSegments = 6, Rings = 6 };
        }
        if (GameObject != null) {
            return GameObject.AddChildAsync(meshNode, GameObject.Owner ?? GameObject);
        }
        return Task.CompletedTask;
    }

    public Aabb GetBounds()
    {
        var meshnode = GetOrFindMeshNode();
        if (meshnode == null) return new Aabb();
        return GameObject.Transform * meshnode.GetNode3DAABB(false);
    }
}