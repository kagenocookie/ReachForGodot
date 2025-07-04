namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using ReaGE.EFX;
using ReeLib;

[GlobalClass, Tool, ResourceHolder("efx", KnownFileFormats.Effect), Icon("res://addons/ReachForGodot/icons/Efx.png")]
public partial class EfxResource : REResourceProxy, IExportableAsset
{
    public EfxResource() : base(KnownFileFormats.Effect) { }

    public PackedScene? EfxScene => ImportedResource as PackedScene;
    public EfxRootNode? Instantiate() => EfxScene?.Instantiate<EfxRootNode>();

    [Export] public Dictionary<string, uint>? BoneValues { get; set; }

    protected override async Task<Resource?> Import()
    {
        await CreateImporter().Efx.ImportFromFile(this);
        NotifyPropertyListChanged();
        return ImportedResource;
    }

    public override Resource? GetOrCreatePlaceholder(GodotImportOptions options)
    {
        return ImportedResource ??= CreateImporter(options).Efx.CreateScenePlaceholder(this);
    }
}
