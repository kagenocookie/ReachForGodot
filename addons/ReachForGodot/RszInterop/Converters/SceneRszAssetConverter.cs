namespace ReaGE;

using System.Threading.Tasks;
using Godot;

public abstract class SceneRszAssetConverter<TResource, TFile, TRootNode> : RszAssetConverter<TResource, TFile, PackedScene, TRootNode>
    where TRootNode : Node, IImportableAsset, new()
    where TResource : REResourceProxy, new()
    where TFile : RszTool.BaseFile
{
    public override PackedScene? GetImportedAssetFromResource(TResource resource) => resource.ImportedResource as PackedScene;
    public override TRootNode? GetInstanceFromAsset(PackedScene? asset) => asset?.Instantiate<TRootNode>();
    public PackedScene CreateScenePlaceholder(TResource target) => base.CreateScenePlaceholder<TRootNode>(target);
    protected override TRootNode CreateInstance(TResource resource) => CreateScenePlaceholder(resource).Instantiate<TRootNode>();
    protected override void PostImport(TResource resource, TRootNode instance)
    {
        if (resource.ImportedResource == null) {
            resource.ImportedResource = resource.Asset == null ? instance.ToPackedScene() : CreateOrReplaceSceneResource(instance, resource.Asset);
        } else if (instance == EditorInterface.Singleton.GetEditedSceneRoot()) {
            EditorInterface.Singleton.MarkSceneAsUnsaved();
            if (WritesEnabled) {
                if (string.IsNullOrEmpty(instance.SceneFilePath) || instance.SceneFilePath != resource.ImportedResource.ResourcePath) {
                    resource.ImportedResource = resource.Asset == null ? instance.ToPackedScene() : CreateOrReplaceSceneResource(instance, resource.Asset);
                }
            } else {
                resource.ImportedResource ??= instance.ToPackedScene();
            }
        }
    }
}
