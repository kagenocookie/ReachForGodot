namespace RGE;

using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, Tool, REComponentClass("via.render.Mesh")]
public partial class REMeshComponent : REComponent
{
    [Export] public Node3D? meshNode;

    [Export] public string? MeshFilepath { get; set; }

    [ExportToolButton("Reinstantiate mesh")]
    private Callable ForceReinstance => Callable.From(FindResourceAndReinit);

    private MeshResource? GetMeshResource() => SerializedContainers.Select(parent => parent.FindResource<MeshResource>(MeshFilepath)).FirstOrDefault();

    private void FindResourceAndReinit()
    {
        if (GameObject != null) {
            _ = ReloadMesh(GetMeshResource(), GameObject, true);
        }
    }

    public override void _ExitTree()
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

    private string MeshField => Data?.TypeInfo.GetFieldNameOrFallback("Mesh", static (f) => f.VariantType == Variant.Type.String) ?? string.Empty;

    public override Task Setup(IRszContainerNode root, REGameObject gameObject, RszInstance rsz, RszImportType importType)
    {
        MeshFilepath = rsz.GetFieldValue(MeshField) as string;
        if (string.IsNullOrEmpty(MeshFilepath)) {
            meshNode?.QueueFree();
            meshNode = null;
            return Task.CompletedTask;
        }

        if (importType == RszImportType.Placeholders || importType == RszImportType.Import && meshNode != null) {
            return Task.CompletedTask;
        }

        return ReloadMesh(root.FindResource<MeshResource>(MeshFilepath), gameObject, importType == RszImportType.ForceReimport);
    }

    public override void PreExport()
    {
        base.PreExport();
        Debug.Assert(Data != null);
        var resource = Importer.FindImportedResourceAsset(meshNode?.SceneFilePath) as MeshResource;
        var meshScenePath = resource?.Asset?.NormalizedFilepath;

        Data.Set("Mesh", meshScenePath ?? string.Empty);
    }

    protected async Task ReloadMesh(MeshResource? mr, REGameObject gameObject, bool forceReload)
    {
        if (mr != null) {
            var res = await mr.Import(forceReload).ContinueWith(static (t) => t.IsFaulted ? null : t.Result);
            await ReinstantiateMesh(res as PackedScene, gameObject);
        } else {
            meshNode?.QueueFree();
            meshNode = null;
            GD.Print("Missing mesh " + MeshFilepath + " at path: " + gameObject.Owner.GetPathTo(gameObject));
        }
    }

    public Task ReinstantiateMesh(PackedScene? scene, REGameObject? go)
    {
        meshNode?.Free();
        if (scene != null) {
            meshNode = scene.Instantiate<Node3D>(PackedScene.GenEditState.Instance);
            meshNode.Name = "__" + meshNode.Name;
            if (go != null) {
                return go.AddChildAsync(meshNode, Owner);
            }
        }
        return Task.CompletedTask;
    }
}