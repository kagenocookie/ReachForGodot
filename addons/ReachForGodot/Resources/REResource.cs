namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class REResource : REObject, IAssetPointer
{
    [Export] public AssetReference? Asset { get; set; }
    [Export] public RESupportedFileFormats ResourceType { get; set; } = RESupportedFileFormats.Unknown;
}
