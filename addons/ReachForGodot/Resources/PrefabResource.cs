namespace ReaGE;

using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool, ResourceHolder("pfb", SupportedFileFormats.Prefab)]
public partial class PrefabResource : REResourceProxy, IImportableAsset, IExportableAsset
{
    public PackedScene? Scene => ImportedResource as PackedScene;
    public PrefabNode? Instantiate() => Scene?.Instantiate<PrefabNode>();

    public PrefabResource() : base(SupportedFileFormats.Prefab)
    {
    }

    protected override async Task<Resource?> Import()
    {
        await CreateImporter().Pfb.ImportFromFile(this);
        NotifyPropertyListChanged();
        return ImportedResource;
    }

    public override Resource? GetOrCreatePlaceholder(GodotImportOptions options)
    {
        return ImportedResource ??= CreateImporter(options).Pfb.CreateScenePlaceholder(this);
    }

    IEnumerable<(string label, GodotImportOptions importMode)> IImportableAsset.SupportedImportTypes => PrefabNode.ImportTypes;
}
