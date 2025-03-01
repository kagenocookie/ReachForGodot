namespace RGE;

using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, Tool, REComponentClass("via.render.Mesh")]
public partial class REMeshComponent : REComponent, IVisualREComponent
{
    private Node3D? meshNode;
    private REField MeshField => TypeInfo.GetFieldOrFallback("Mesh", static (f) => f.VariantType == Variant.Type.String);
    public string? MeshFilepath => TryGetFieldValue(MeshField, out var path) ? path.AsString() : null;

    [ExportToolButton("Reinstantiate mesh")]
    private Callable ForceReinstance => Callable.From(FindResourceAndReinit);

    public Node3D? GetOrFindMeshNode()
    {
        meshNode ??= GameObject.FindChildWhere<Node3D>(child => child is not REGameObject && child.Name.ToString().StartsWith("__"));
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

    public override Task Setup(REGameObject gameObject, RszInstance rsz, RszImportType importType)
    {
        GameObject = gameObject;
        meshNode ??= GetOrFindMeshNode();
        var path = MeshFilepath;
        if (string.IsNullOrEmpty(path)) {
            meshNode?.QueueFree();
            meshNode = null;
            return Task.CompletedTask;
        }

        if (importType == RszImportType.Placeholders || importType == RszImportType.CreateOrReuse && meshNode != null) {
            return Task.CompletedTask;
        }

        return ReloadMesh(Importer.FindOrImportResource<MeshResource>(path, ReachForGodot.GetAssetConfig(gameObject.Game)), importType == RszImportType.ForceReimport);
    }

    public override void PreExport()
    {
        base.PreExport();
        var resource = Importer.FindImportedResourceAsset(meshNode?.SceneFilePath) as MeshResource;
        var meshScenePath = resource?.Asset?.AssetFilename;

        SetField("Mesh", meshScenePath ?? string.Empty);
    }

    protected async Task ReloadMesh(MeshResource? mr, bool forceReload)
    {
        if (mr != null) {
            var res = await mr.Import(forceReload).ContinueWith(static (t) => t.IsFaulted ? null : t.Result);
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