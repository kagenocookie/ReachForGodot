namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class REResource : Resource, IAssetPointer
{
    [Export] public SupportedGame Game { get; set; }
    [Export] public AssetReference? Asset { get; set; }
    public RESupportedFileFormats ResourceType { get; protected set; } = RESupportedFileFormats.Unknown;

    public REResource() { }
    protected REResource(RESupportedFileFormats format) => ResourceType = format;
}
