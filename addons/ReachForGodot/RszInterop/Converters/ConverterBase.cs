namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using RszTool;

public abstract class ConverterBase<TImported, TExported, TResource>
    where TImported : GodotObject
    where TResource : Resource
{
    public AssetConverter Convert { get; set; } = null!;
    public SupportedGame Game => Convert.Game;
    public AssetConfig Config => Convert.AssetConfig;
    public bool WritesEnabled => Convert.Options.allowWriting;

    public virtual TImported? GetResourceImportedObject(TResource resource) => resource != null ? resource as TImported ?? throw new NotImplementedException() : null;

    public TResource CreateOrReplaceResourcePlaceholder(string resolvedFilepath)
    {
        var instance = CreateOrReplaceResourcePlaceholder(new AssetReference(PathUtils.FullToRelativePath(resolvedFilepath, Config) ?? resolvedFilepath));
        return instance;
    }

    public abstract TResource CreateOrReplaceResourcePlaceholder(AssetReference reference);

    public virtual void Clear()
    {
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
        root.Name = path.BaseFilename.ToString();
        var scene = root.ToPackedScene(false);
        var importFilepath = path.GetImportFilepath(Config);
        if (importFilepath == null) return scene;

        return Convert.Options.allowWriting ? SaveOrReplaceResource(scene, importFilepath) : scene;
    }

    protected TRes SaveOrReplaceResource<TRes>(TRes newResource, string importFilepath) where TRes : Resource
    {
        if (ResourceLoader.Exists(importFilepath)) {
            newResource.TakeOverPath(importFilepath);
        } else {
            Directory.CreateDirectory(ProjectSettings.GlobalizePath(importFilepath.GetBaseDir()));
            newResource.ResourcePath = importFilepath;
        }
        Log("Saving resource " + importFilepath);
        var status = ResourceSaver.Save(newResource);
        if (status != Error.Ok) {
            ErrorLog($"Failed to save resource {importFilepath}:\n{status}");
        }
        Convert.AddResource(importFilepath, newResource);
        Importer.QueueFileRescan();
        return newResource;
    }
}

public abstract class DataConverter<TImported, TExported, TResource> : ConverterBase<TImported, TExported, TResource>
    where TImported : GodotObject
    where TResource : Resource
{
    public abstract Task<bool> Import(TExported file, TImported target);
    public abstract Task<bool> Export(TImported source, TExported file);

    public bool ExportSync(TImported source, TExported file)
    {
        var task = Export(source, file);
        // note: might lock up in some situations?
        task.Wait();
        return task.Result;
    }

    protected static bool PostExport(bool success, string outputFile)
    {
        if (!success && File.Exists(outputFile) && new FileInfo(outputFile).Length == 0) {
            File.Delete(outputFile);
        }

        return success;
    }
}
