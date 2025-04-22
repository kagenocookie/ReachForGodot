namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using RszTool;

public abstract class RszToolConverter<TResource, TExported, TAsset> : DataConverter<TResource, TExported, TAsset>
    where TAsset : GodotObject
    where TExported : BaseFile
    where TResource : REResource
{
    public async Task<bool> ExportToFile(TAsset resource, string outputPath)
    {
        Clear();
        var file = CreateFile(new FileHandler(outputPath));
        await Export(resource, file);
        Clear();
        return PostExport(file.Save(), outputPath);
    }

    public Task<bool> ImportFromFile<T>(T imported) where T : IImportableAsset, TAsset
    {
        Convert.Game = imported.Game;
        var fn = imported.Asset?.FindSourceFile(Config);
        if (string.IsNullOrEmpty(fn)) return Task.FromResult(false);
        return ImportFromFile(fn, imported);
    }

    public async Task<bool> ImportFromFile(string sourcePath, TAsset? imported = null)
    {
        Clear();
        var file = CreateFile(new FileHandler(sourcePath));
        if (!LoadFile(file)) return false;

        if (imported == null) {
            var resource = Importer.FindOrImportAsset<TResource>(sourcePath, Config, WritesEnabled);
            if (resource == null) {
                GD.PrintErr("Resource could not be created: " + sourcePath);
                return false;
            }
            imported = GetResourceImportedObject(resource);
            if (imported == null) return false;
        }

        if (!await Import(file, imported)) return false;
        // TODO save imported resource
        Clear();
        return true;
    }

    public virtual bool LoadFile(TExported file)
    {
        return file.Read();
    }

    protected PackedScene CreateOrReplaceRszSceneResource<TRoot>(TRoot root, AssetReference path) where TRoot : Node, IRszContainer, new()
    {
        root = SetupRszContainer(root, path);
        return CreateOrReplaceSceneResource(root, path);
    }

    public TExported CreateFile(string absoluteFilepath) => CreateFile(new FileHandler(absoluteFilepath));
    public TExported CreateFile(Stream stream, int fileVersion) => CreateFile(new FileHandler(stream) { FileVersion = fileVersion });

    public abstract TExported CreateFile(FileHandler fileHandler);
}

public abstract class ResourceConverter<TResource, TExported> : RszToolConverter<TResource, TExported, TResource>
    where TResource : REResource
    where TExported : BaseFile
{
}

public interface ISynchronousConverter<TResource, TFile>
{
    TFile CreateFile(string absoluteFilepath);
    bool LoadFile(TFile file);
    bool ImportSync(TFile file, TResource target);
}
