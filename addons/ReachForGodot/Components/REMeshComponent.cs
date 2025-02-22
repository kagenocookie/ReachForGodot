namespace RGE;

using System;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;
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

    public override Task Setup(IRszContainerNode root, REGameObject gameObject, RszInstance rsz, RszImportType importType)
    {
        MeshFilepath = rsz.GetFieldValue("v2") as string ?? rsz.GetFieldValue("v20") as string ?? rsz.Values.FirstOrDefault(v => v is string) as string;
        if (importType == RszImportType.Placeholders || importType == RszImportType.Import && meshNode != null) {
            return Task.CompletedTask;
        }

        return ReloadMesh(root.FindResource<MeshResource>(MeshFilepath), gameObject, importType == RszImportType.ForceReimport);
    }

    protected async Task ReloadMesh(MeshResource? mr, REGameObject gameObject, bool forceReload)
    {
        if (mr != null) {

            var res = await mr.Import(forceReload).ContinueWith(static (t) => t.IsFaulted ? null : t.Result);
            await ReinstantiateMesh(res as PackedScene, gameObject);
        } else {
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