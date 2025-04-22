namespace ReaGE;

using System.Threading.Tasks;
using Godot;

public abstract class ConverterBase<TResource, TExported, TAsset>
    where TAsset : GodotObject
    where TResource : Resource
{
    public AssetConverter Convert { get; set; } = null!;
    public SupportedGame Game => Convert.Game;
    public AssetConfig Config => Convert.AssetConfig;
    public bool WritesEnabled => Convert.Options.allowWriting;

    public virtual TAsset? GetResourceImportedObject(TResource resource) => resource != null ? resource as TAsset ?? throw new NotImplementedException() : null;

    public TResource CreateOrReplaceResourcePlaceholder(string resolvedFilepath)
    {
        var instance = CreateOrReplaceResourcePlaceholder(new AssetReference(PathUtils.FullToRelativePath(resolvedFilepath, Config) ?? resolvedFilepath));
        return instance;
    }

    public abstract TResource CreateOrReplaceResourcePlaceholder(AssetReference reference);

    public virtual void Clear()
    {
    }

    public bool ImportSync<TImportable>(TImportable resource) where TImportable : REResource, TResource
    {
        if (this is not ISynchronousConverter<TResource, TExported> sync) {
            GD.PrintErr("Resource does not have a synchronous conversion: " + resource.ResourceType);
            return false;
        }

        var source = resource.Asset?.FindSourceFile(Config);
        if (source == null) return false;

        var file = sync.CreateFile(source);
        try {
            sync.LoadFile(file);
            var success = sync.ImportSync(file, resource);
            return success;
        } finally {
            Clear();
            (file as IDisposable)?.Dispose();
        }
    }

    protected TRes SetupResource<TRes>(TRes resource, AssetReference reference) where TRes : REResource
    {
        resource.Game = Game;
        resource.Asset = reference;
        return resource;
    }

    protected TRes SetupRszContainer<TRes>(TRes resource, AssetReference reference) where TRes : IRszContainer
    {
        resource.Game = Game;
        resource.Asset = reference;
        return resource;
    }

    protected void Log(string text)
    {
        if (Convert.Options.logInfo) GD.Print(text);
    }

    protected void ErrorLog(string text, object? context = null)
    {
        if (Convert.Options.logErrors) {
            if (context != null) {
                GD.PrintErr(text, context);
            } else {
                GD.PrintErr(text);
            }
        }
    }

    protected PackedScene CreateOrReplaceSceneResource<TRoot>(TRoot root, AssetReference path) where TRoot : Node, new()
    {
        root.Name = path.BaseFilename.ToString().StringOrDefault("Unnamed");
        var scene = root.ToPackedScene(false);
        var importFilepath = PathUtils.GetAssetImportPath(path.AssetFilename, PathUtils.GetFileFormat(path.AssetFilename).format, Config);
        if (importFilepath == null) return scene;

        return Convert.Options.allowWriting ? SaveOrReplaceResource(scene, importFilepath) : scene;
    }

    protected TRes SaveOrReplaceResource<TRes>(TRes newResource, string importFilepath) where TRes : Resource
    {
        Log("Saving resource " + importFilepath);
        Convert.AddResource(importFilepath, newResource);
        var status = newResource.SaveOrReplaceResource(importFilepath);
        if (status == Error.Ok) {
            Importer.QueueFileRescan();
        } else {
            ErrorLog($"Failed to save resource {importFilepath}:\n{status}");
        }
        return newResource;
    }
}

public abstract class DataConverter<TResource, TExported, TAsset> : ConverterBase<TResource, TExported, TAsset>
    where TAsset : GodotObject
    where TResource : Resource
{
    public abstract Task<bool> Import(TExported file, TAsset target);
    public abstract Task<bool> Export(TAsset source, TExported file);

    protected static bool PostExport(bool success, string outputFile)
    {
        if (!success && File.Exists(outputFile) && new FileInfo(outputFile).Length == 0) {
            File.Delete(outputFile);
        }

        return success;
    }
}
