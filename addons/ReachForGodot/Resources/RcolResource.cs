namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using ReeLib;

[GlobalClass, Tool, ResourceHolder("rcol", KnownFileFormats.RequestSetCollider)]
public partial class RcolResource : REResourceProxy, IExportableAsset
{
    public PackedScene? RcolScene => ImportedResource as PackedScene;
    public RcolRootNode? Instantiate() => RcolScene?.Instantiate<RcolRootNode>();

    public RcolResource() : base(KnownFileFormats.RequestSetCollider)
    {
    }

    protected override async Task<Resource?> Import()
    {
        await CreateImporter().Rcol.ImportFromFile(this);
        NotifyPropertyListChanged();
        return ImportedResource;
    }

    public override Resource? GetOrCreatePlaceholder(GodotImportOptions options)
    {
        return ImportedResource ??= CreateImporter(options).Rcol.CreateScenePlaceholder(this);
    }
}
