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

    public virtual TAsset? GetImportedAssetFromResource(TResource resource)
        => resource != null ? resource as TAsset ?? throw new NotImplementedException() : null;

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

    protected TRes SetupResource<TRes>(TRes resource, AssetReference reference) where TRes : IAssetPointer
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
        root.Name = path.BaseFilename.StringOrDefault("Unnamed");
        var scene = root.ToPackedScene(false);
        var importFilepath = PathUtils.GetAssetImportPath(path.AssetFilename, PathUtils.ParseFileFormat(path.AssetFilename).format, Config);
        if (importFilepath == null) return scene;

        if (Convert.Options.allowWriting) {
            var status = scene.SaveOrReplaceResource(importFilepath);
            if (status != Error.Ok) {
                ErrorLog($"Failed to save scene {importFilepath}:\n{status}");
            }
        }
        return scene;
    }
}

public abstract class DataConverter<TResource, TExported, TAsset, TAssetInstance> : ConverterBase<TResource, TExported, TAsset>
    where TAsset : GodotObject
    where TAssetInstance : GodotObject
    where TResource : REResource, new()
{
    public virtual TAssetInstance? GetInstanceFromAsset(TAsset? asset)
        => typeof(TAsset) == typeof(TAssetInstance) ? asset as TAssetInstance :
        asset is PackedScene scene ? scene.Instantiate<TAssetInstance>()
        : throw new NotImplementedException();

    public abstract Task<bool> Import(TExported file, TAssetInstance target);
    public abstract Task<bool> Export(TAssetInstance source, TExported file);

    public override TResource CreateOrReplaceResourcePlaceholder(AssetReference reference)
    {
        return SetupResource(new TResource(), reference);
    }

    protected static bool PostExport(bool success, string outputFile)
    {
        if (!success && File.Exists(outputFile) && new FileInfo(outputFile).Length == 0) {
            File.Delete(outputFile);
        }

        return success;
    }
}
