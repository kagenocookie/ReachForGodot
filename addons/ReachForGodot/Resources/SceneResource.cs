namespace ReaGE;

using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool, ResourceHolder("scn", SupportedFileFormats.Scene)]
public partial class SceneResource : REResourceProxy, IExportableAsset
{
    public PackedScene? Scene => ImportedResource as PackedScene;
    public SceneFolder? Instantiate() => Scene?.Instantiate<SceneFolder>();

    public SceneResource() : base(SupportedFileFormats.Scene)
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
}
