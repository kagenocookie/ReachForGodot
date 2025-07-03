namespace ReaGE;

using Godot;
using ReeLib;

[GlobalClass, Tool]
public partial class REResource : Resource, IAssetPointer
{
    [Export] public SupportedGame Game { get; set; }
    [Export] public AssetReference? Asset { get; set; }

    public AssetConfig Config => ReachForGodot.GetAssetConfig(Game);
    public KnownFileFormats ResourceType { get; protected set; } = KnownFileFormats.Unknown;

    protected AssetConverter CreateImporter() => CreateImporter(GodotImportOptions.importTreeChanges);
    protected AssetConverter CreateImporter(GodotImportOptions options) => new AssetConverter(Game, options);

    public REResource() { }
    protected REResource(KnownFileFormats format) => ResourceType = format;
}
