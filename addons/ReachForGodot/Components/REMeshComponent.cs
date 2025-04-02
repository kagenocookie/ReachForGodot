namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, Tool, REComponentClass("via.render.Mesh")]
public partial class REMeshComponent : REComponent, IVisualREComponent
{
    private static readonly REFieldAccessor MeshField = new REFieldAccessor("Mesh")
        .Resource<MeshResource>()
        .Conditions((fields) => fields.FirstOrDefault(f => f.RszField.type is RszFieldType.String or RszFieldType.Resource));

    private static readonly REFieldAccessor MaterialField = new REFieldAccessor("Material")
        .Resource<MaterialDefinitionResource>()
        .Conditions((fields) => fields.Where(f => f.RszField.type is RszFieldType.String or RszFieldType.Resource).Skip(1).FirstOrDefault());

    private Node3D? meshNode;
    public MaterialDefinitionResource? Material => TryGetFieldValue(MaterialField.Get(this), out var path) ? path.As<MaterialDefinitionResource>() : null;
    public MeshResource? Mesh => TryGetFieldValue(MeshField.Get(this), out var path) ? path.As<MeshResource>() : null;

    [ExportToolButton("Import & reinstantiate mesh")]
    private Callable ForceReinstance => Callable.From(FindResourceAndReinit);

    public override void PreExport()
    {
        if (meshNode != null && IsInstanceValid(meshNode) && !meshNode.Transform.IsEqualApprox(Transform3D.Identity)) {
            GD.PrintErr("Detected movement in mesh component - move the parent GameObject instead: " + Path);
            meshNode.SetIdentity();
        }
    }

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
        _ = ReloadMesh(false);
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
        return Mesh?.ResourcePath == mr.ResourcePath;
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
            EnsureResourceInContainer(value.As<MaterialDefinitionResource>());
        }
        return r;
    }

    public override async Task Setup(RszImportType importType)
    {
        meshNode ??= GetOrFindMeshNode();
        if (Mesh == null) {
            meshNode?.QueueFree();
            meshNode = null;
            return;
        }

        if (importType == RszImportType.Placeholders) return;

        await ReloadMesh(importType >= RszImportType.Reimport);
    }

    protected async Task ReloadMesh(bool forceReload)
    {
        if (Material is MaterialDefinitionResource mdf) {
            await mdf.EnsureNotEmpty();
        }
        if (Mesh is MeshResource mr) {
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
            meshNode = scene.Instantiate<Node3D>(PackedScene.GenEditState.Disabled);
            meshNode.Name = "__" + meshNode.Name;
        } else {
            var mi = new MeshInstance3D() { Name = "__Mesh" };
            meshNode = mi;
            mi.Mesh = new SphereMesh() { Radius = 0.5f, Height = 1, RadialSegments = 6, Rings = 6 };
        }
        if (GameObject != null) {
            return GameObject.AddChildAsync(meshNode, GameObject.FindRszOwnerNode());
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