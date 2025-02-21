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
            _ = ReloadMesh(GetMeshResource(), GameObject);
        }
    }

    private bool IsCorrectMesh(MeshResource mr)
    {
        return MeshFilepath != null && mr.Asset?.IsSameAsset(MeshFilepath) == true;
    }

    public override Task Setup(IRszContainerNode root, REGameObject gameObject, RszInstance rsz)
    {
        MeshFilepath = rsz.GetFieldValue("v2") as string ?? rsz.GetFieldValue("v20") as string ?? rsz.Values.FirstOrDefault(v => v is string) as string;

        return ReloadMesh(root.FindResource<MeshResource>(MeshFilepath), gameObject);
    }

    protected async Task ReloadMesh(MeshResource? mr, REGameObject gameObject)
    {
        if (mr != null) {
            var res = await mr.Import(false).ContinueWith(static (t) => t.IsFaulted ? null : t.Result);
            ReinstantiateMesh(res as PackedScene, gameObject);
        } else {
            meshNode = null;
            GD.Print("Missing mesh " + MeshFilepath + " at path: " + gameObject.Owner.GetPathTo(gameObject));
        }
    }

    public void ReinstantiateMesh(PackedScene? scene, REGameObject? go)
    {
        meshNode?.Free();
        if (scene != null) {
            meshNode = scene.Instantiate<Node3D>(PackedScene.GenEditState.Instance);
            meshNode.Name = "__" + meshNode.Name;
            go?.AddDeferredChild(meshNode, Owner);
        }
    }
}