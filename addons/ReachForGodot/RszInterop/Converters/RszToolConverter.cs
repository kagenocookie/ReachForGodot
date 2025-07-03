namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using ReeLib;

public abstract class ReeLibConverter<TResource, TExported, TAsset, TAssetInstance> : DataConverter<TResource, TExported, TAsset, TAssetInstance>
    where TAsset : GodotObject
    where TAssetInstance : GodotObject
    where TExported : BaseFile
    where TResource : REResource, new()
{
    public async Task<bool> ExportToFile(TAssetInstance instance, string outputPath)
    {
        Clear();
        using var file = CreateFile(new FileHandler(outputPath));
        var success = await Export(instance, file);
        Clear();
        return success && PostExport(file.Save(), outputPath);
    }

    public Task<bool> ImportFromFile(TResource resource)
    {
        var fn = resource.Asset?.FindSourceFile(Config);
        if (string.IsNullOrEmpty(fn)) return Task.FromResult(false);

        return ImportFromFile(fn, resource);
    }

    public Task<bool> ImportFromFile(string sourceFile)
    {
        var resource = Importer.FindOrImportResource<TResource>(sourceFile, Config, WritesEnabled);
        if (resource == null) {
            ErrorLog("Failed to create resource: " + sourceFile);
            return Task.FromResult(false);
        }

        var imported = GetInstanceFromAsset(GetImportedAssetFromResource(resource))
            ?? CreateInstance(resource);

        return ImportFromFile(sourceFile, resource, imported);
    }

    public Task<bool> ImportFromFile(string sourceFile, TResource resource)
    {
        Convert.Game = resource.Game;
        var imported = GetInstanceFromAsset(GetImportedAssetFromResource(resource))
            ?? CreateInstance(resource);

        return ImportFromFile(sourceFile, resource, imported);
    }

    public Task<bool> ImportFromFile<T>(T imported) where T : IImportableAsset, TAssetInstance
    {
        Convert.Game = imported.Game;
        var fn = imported.Asset?.FindSourceFile(Config);
        if (string.IsNullOrEmpty(fn)) return Task.FromResult(false);
        return ImportFromFileToInstance(fn, imported);
    }

    public async Task<bool> ImportFromFileToInstance(string sourcePath, TAssetInstance? imported = null)
    {
        if (imported != null && typeof(TAssetInstance) == typeof(TResource)) {
            return await ImportFromFile(sourcePath, (imported as TResource)!, imported);
        }

        var resource = Importer.FindOrImportResource<TResource>(sourcePath, Config, WritesEnabled);
        if (resource == null) {
            GD.PrintErr("Resource could not be created: " + sourcePath);
            return false;
        }
        imported ??= resource as TAssetInstance;
        if (imported == null) {
            imported = CreateInstance(resource);
            if (imported == null) return false;
        }
        return await ImportFromFile(sourcePath, resource, imported);
    }

    protected async Task<bool> ImportFromFile(string sourcePath, TResource resource, TAssetInstance instance)
    {
        var file = CreateFile(new FileHandler(sourcePath));
        if (!LoadFile(file)) return false;
        if (!await Import(file, instance)) return false;
        PostImport(resource, instance);
        Clear();
        return true;
    }

    protected virtual void PostImport(TResource resource, TAssetInstance instance) { }

    protected virtual TAssetInstance CreateInstance(TResource resource) => throw new NotImplementedException();

    protected PackedScene CreateScenePlaceholder<TRootNode>(TResource target) where TRootNode : Node, TAssetInstance, IAssetPointer, new()
    {
        Debug.Assert(target.Asset != null);

        var root = new TRootNode() {
            Asset = target.Asset.Clone(),
            Name = target.ResourceName ?? target.Asset.BaseFilename.ToString(),
            Game = target.Game,
        };
        root.LockNode(true);
        PreCreateScenePlaceholder(root, target);
        var scene = new PackedScene();
        scene.Pack(root);
        if (target is REResourceProxy proxy) proxy.ImportedResource = scene;
        if (WritesEnabled) {
            var resourcePath = target.Asset.GetImportFilepath(Config);
            var scenePath = PathUtils.GetAssetImportPath(target.Asset.ExportedFilename, target.ResourceType, Config);
            if (scenePath != null) scene.SaveOrReplaceResource(scenePath);
            if (resourcePath != null) target.SaveOrReplaceResource(resourcePath);
        }

        return scene;
    }
    protected virtual void PreCreateScenePlaceholder(TAssetInstance node, TResource target) { }

    public virtual bool LoadFile(TExported file)
    {
        return file.Read();
    }

    public TExported CreateFile(string absoluteFilepath) => CreateFile(new FileHandler(absoluteFilepath));
    public TExported CreateFile(Stream stream, int fileVersion) => CreateFile(new FileHandler(stream) { FileVersion = fileVersion });
    public TExported CreateFile(Stream stream, string filepath) => CreateFile(new FileHandler(stream, filepath));

    public abstract TExported CreateFile(FileHandler fileHandler);
}

public abstract class ResourceConverter<TResource, TExported> : ReeLibConverter<TResource, TExported, TResource, TResource>
    where TResource : REResource, new()
    where TExported : BaseFile
{
    public override TResource? GetImportedAssetFromResource(TResource resource) => resource;
    public override TResource? GetInstanceFromAsset(TResource? asset) => asset;
    protected override TResource CreateInstance(TResource resource) => resource;
}

public interface ISynchronousConverter<TResource, TFile>
{
    TFile CreateFile(string absoluteFilepath);
    bool LoadFile(TFile file);
    bool ImportSync(TFile file, TResource target);
}
