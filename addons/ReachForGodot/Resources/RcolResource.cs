namespace ReaGE;

using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool, ResourceHolder("rcol", RESupportedFileFormats.Rcol)]
public partial class RcolResource : REResourceProxy, IExportableAsset
{
    public PackedScene? RcolScene => ImportedResource as PackedScene;
    public RcolRootNode? Instantiate() => RcolScene?.Instantiate<RcolRootNode>();

    public RcolResource() : base(RESupportedFileFormats.Rcol)
    {
    }

    // string IRszContainer.Path => PathUtils.ImportPathToRelativePath(ResourcePath, ReachForGodot.GetAssetConfig(Game)) ?? ResourcePath;

    protected override Task<Resource?> Import()
    {
        CreateImporter().Rcol.ImportFromFile(this);
        NotifyPropertyListChanged();
        return Task.FromResult(ImportedResource);
    }

    public void Clear()
    {
        // __Data.Clear();
    }
}
