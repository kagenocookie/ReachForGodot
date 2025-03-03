namespace RGE;

using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, Tool, REComponentClass("via.render.Mesh")]
public partial class REMeshComponent : REComponent, IVisualREComponent
{
    private static readonly REObjectFieldAccessor MeshField = new REObjectFieldAccessor("Mesh", (fields) => fields.FirstOrDefault(f => f.VariantType == Variant.Type.String));
    private static readonly REObjectFieldAccessor MaterialField = new REObjectFieldAccessor("Material", (fields) => fields.Where(f => f.VariantType == Variant.Type.String).Skip(1).FirstOrDefault());

    private Node3D? meshNode;
    public string? MeshFilepath => TryGetFieldValue(MeshField.Get(this), out var path) ? path.AsString() : null;

    [ExportToolButton("Reinstantiate mesh")]
    private Callable ForceReinstance => Callable.From(FindResourceAndReinit);

    public Node3D? GetOrFindMeshNode()
    {
        meshNode ??= GameObject.FindChildWhere<Node3D>(child => child.GetType() == typeof(Node3D) && child.Name.ToString().StartsWith("__"));
        if (!IsInstanceValid(meshNode)) {
            meshNode = null;
        }
        return meshNode;
    }

    private MeshResource? GetMeshResource() => SerializedContainers.Select(parent => parent.FindResource<MeshResource>(MeshFilepath)).FirstOrDefault();

    private void FindResourceAndReinit()
    {
        _ = ReloadMesh(GetMeshResource(), true);
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
        return MeshFilepath != null && mr.Asset?.IsSameAsset(MeshFilepath) == true;
    }

    public override async Task Setup(REGameObject gameObject, RszInstance rsz, RszImportType importType)
    {
        GameObject = gameObject;
        meshNode ??= GetOrFindMeshNode();
        var path = MeshFilepath;
        if (string.IsNullOrEmpty(path)) {
            meshNode?.QueueFree();
            meshNode = null;
            return;
        }

        if (importType == RszImportType.Placeholders || importType == RszImportType.CreateOrReuse && meshNode != null) {
            return;
        }

        await ReloadMesh(Importer.FindOrImportResource<MeshResource>(path, ReachForGodot.GetAssetConfig(gameObject.Game)), importType == RszImportType.ForceReimport);
    }

    public override void PreExport()
    {
        meshNode ??= GetOrFindMeshNode();
        var resource = Importer.FindImportedResourceAsset(meshNode?.SceneFilePath) as MeshResource;
        var meshScenePath = resource?.Asset?.AssetFilename;
        var name = GameObject.Name.ToString();

        SetField(MeshField.Get(this), meshScenePath ?? string.Empty);
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
            GD.Print("Missing mesh " + MeshFilepath + " at path: " + GameObject.Owner.GetPathTo(GameObject));
        }
    }

    public Task ReinstantiateMesh(PackedScene? scene)
    {
        meshNode?.Free();
        if (scene != null) {
            meshNode = scene.Instantiate<Node3D>(PackedScene.GenEditState.Instance);
            meshNode.Name = "__" + meshNode.Name;
        } else {
            var mi = new MeshInstance3D() { Name = "__Mesh" };
            meshNode = mi;
            mi.Mesh = new SphereMesh() { Radius = 0.5f, Height = 1 };
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
        return meshnode.GetNode3DAABB(false);
    }
}