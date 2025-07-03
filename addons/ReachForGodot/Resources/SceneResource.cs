namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using ReeLib;

[GlobalClass, Tool, ResourceHolder("scn", KnownFileFormats.Scene)]
public partial class SceneResource : REResourceProxy, IImportableAsset, IExportableAsset
{
    public PackedScene? Scene => ImportedResource as PackedScene;
    public SceneFolder? Instantiate() => Scene?.Instantiate<SceneFolder>();

    public SceneResource() : base(KnownFileFormats.Scene)
    {
    }

    protected override async Task<Resource?> Import()
    {
        await CreateImporter().Scn.ImportFromFile(this);
        NotifyPropertyListChanged();
        return ImportedResource;
    }

    public override Resource? GetOrCreatePlaceholder(GodotImportOptions options)
    {
        return ImportedResource ??= CreateImporter(options).Scn.CreateScenePlaceholder(this);
    }

    IEnumerable<(string label, GodotImportOptions importMode)> IImportableAsset.SupportedImportTypes => SceneFolder.ImportTypes;
}
