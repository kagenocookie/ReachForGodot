namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using RszTool;

public abstract class RszToolConverter<TImported, TExported, TResource> : DataConverter<TImported, TExported, TResource>
    where TImported : GodotObject
    where TExported : BaseFile
    where TResource : Resource
{
    public async Task<bool> ExportToFile(TImported resource, string outputPath)
    {
        Clear();
        var file = CreateFile(new FileHandler(outputPath));
        await Export(resource, file);
        Clear();
        return PostExport(file.Save(), outputPath);
    }

    public Task<bool> ImportFromFile<T>(T imported) where T : IImportableAsset, TImported
    {
        Convert.Game = imported.Game;
        var fn = imported.Asset?.FindSourceFile(Config);
        if (string.IsNullOrEmpty(fn)) return Task.FromResult(false);
        return ImportFromFile(fn, imported);
    }

    public async Task<bool> ImportFromFile(string sourcePath, TImported? imported = null)
    {
        Clear();
        var file = CreateFile(new FileHandler(sourcePath));
        if (!LoadFile(file)) return false;

        if (imported == null) {
            var resource = Importer.FindOrImportResource<TResource>(sourcePath, Config, WritesEnabled);
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

    public virtual void SaveAsset(TImported imported)
    {
        // SaveOrReplaceResource<TImported>(imported, )
    }

    public virtual bool LoadFile(TExported file)
    {
        return file.Read();
    }

    public TExported CreateFile(string absoluteFilepath) => CreateFile(new FileHandler(absoluteFilepath));
    public TExported CreateFile(Stream stream, int fileVersion) => CreateFile(new FileHandler(stream) { FileVersion = fileVersion });

    public abstract TExported CreateFile(FileHandler fileHandler);
}

public abstract class ResourceConverter<TImported, TExported> : RszToolConverter<TImported, TExported, TImported>
    where TImported : REResource
    where TExported : BaseFile
{
}
