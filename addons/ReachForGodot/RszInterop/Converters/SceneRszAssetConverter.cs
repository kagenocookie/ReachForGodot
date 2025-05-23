namespace ReaGE;

using System;
using System.Threading.Tasks;
using Godot;

public abstract class SceneRszAssetConverter<TResource, TFile, TRootNode> : RszAssetConverter<TResource, TFile, TRootNode>
    where TRootNode : Node, IImportableAsset, new()
    where TResource : REResourceProxy, new()
    where TFile : RszTool.BaseFile
{
    public override TRootNode? GetResourceImportedObject(TResource resource) => (resource.ImportedResource as PackedScene)?.Instantiate<TRootNode>();

    public override TResource CreateOrReplaceResourcePlaceholder(AssetReference reference)
    {
        return SetupResource(new TResource(), reference);
    }

    public Task<bool> Export(TResource resource, TFile target)
    {
        return Export((resource.ImportedResource as PackedScene)?.Instantiate<TRootNode>() ?? new TRootNode(), target);
    }

    public PackedScene CreateScenePlaceholder(TResource target)
    {
        Debug.Assert(target.Asset != null);

        var scene = new PackedScene();
        var root = new TRootNode() {
            Asset = target.Asset.Clone(),
            Name = target.ResourceName ?? target.Asset.BaseFilename.ToString(),
            Game = target.Game,
        };
        PreCreateScenePlaceholder(root, target);
        scene.Pack(root);
        target.ImportedResource = scene;
        if (WritesEnabled) {
            var resourcePath = target.Asset.GetImportFilepath(Config);
            var scenePath = PathUtils.GetAssetImportPath(target.Asset.ExportedFilename, target.ResourceType, Config);
            if (scenePath != null) scene.SaveOrReplaceResource(scenePath);
            if (resourcePath != null) target.SaveOrReplaceResource(resourcePath);
        }

        return scene;
    }

    protected virtual void PreCreateScenePlaceholder(TRootNode node, TResource target) { }

    public async Task<bool> ImportFromFile(TResource target)
    {
        var node = (target.ImportedResource as PackedScene)?.Instantiate<TRootNode>();
        if (node == null) {
            node = CreateScenePlaceholder(target).Instantiate<TRootNode>();
        }
        var success = await base.ImportFromFile<TRootNode>(node);
        if (success && WritesEnabled && node.Asset != null) {
            target.ImportedResource = CreateOrReplaceSceneResource(node, node.Asset);
            target.SaveOrReplaceResource(target.ResourcePath);
        }
        return success;
    }
}
