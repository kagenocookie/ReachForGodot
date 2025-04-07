namespace ReaGE;

using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool, ResourceHolder("rcol", SupportedFileFormats.Rcol)]
public partial class RcolResource : REResourceProxy, IExportableAsset
{
    public PackedScene? RcolScene => ImportedResource as PackedScene;
    public RcolRootNode? Instantiate() => RcolScene?.Instantiate<RcolRootNode>();

    public RcolResource() : base(SupportedFileFormats.Rcol)
    {
    }

    protected override async Task<Resource?> Import()
    {
        await CreateImporter().Rcol.ImportFromFile(this);
        NotifyPropertyListChanged();
        return ImportedResource;
    }
}
