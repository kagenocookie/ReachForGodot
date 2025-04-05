namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class REResource : Resource, IAssetPointer
{
    [Export] public SupportedGame Game { get; set; }
    [Export] public AssetReference? Asset { get; set; }

    public AssetConfig Config => ReachForGodot.GetAssetConfig(Game);
    public SupportedFileFormats ResourceType { get; protected set; } = SupportedFileFormats.Unknown;

    protected AssetConverter CreateImporter() => CreateImporter(GodotImportOptions.importTreeChanges);
    protected AssetConverter CreateImporter(GodotImportOptions options) => new AssetConverter(Game, options);

    public REResource() { }
    protected REResource(SupportedFileFormats format) => ResourceType = format;
}
