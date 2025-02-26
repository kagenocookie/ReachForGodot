namespace RGE;

using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, Tool, REComponentClass("via.render.Mesh")]
public partial class REMeshComponent : REComponent
{
    public Node3D? meshNode;
    private REField MeshField => TypeInfo.GetFieldOrFallback("Mesh", static (f) => f.VariantType == Variant.Type.String);
    public string? MeshFilepath => TryGetFieldValue(MeshField, out var path) ? path.AsString() : null;

    [ExportToolButton("Reinstantiate mesh")]
    private Callable ForceReinstance => Callable.From(FindResourceAndReinit);

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

    public override Task Setup(IRszContainerNode root, REGameObject gameObject, RszInstance rsz, RszImportType importType)
    {
        GameObject = gameObject;
        var path = MeshFilepath;
        if (string.IsNullOrEmpty(path)) {
            meshNode?.QueueFree();
            meshNode = null;
            return Task.CompletedTask;
        }

        if (importType == RszImportType.Placeholders || importType == RszImportType.Import && meshNode != null) {
            return Task.CompletedTask;
        }

        return ReloadMesh(root.FindResource<MeshResource>(path), importType == RszImportType.ForceReimport);
    }

    public override void PreExport()
    {
        base.PreExport();
        var resource = Importer.FindImportedResourceAsset(meshNode?.SceneFilePath) as MeshResource;
        var meshScenePath = resource?.Asset?.NormalizedFilepath;

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
            if (GameObject != null) {
                return GameObject.AddChildAsync(meshNode, GameObject.Owner ?? GameObject);
            }
        }
        return Task.CompletedTask;
    }
}