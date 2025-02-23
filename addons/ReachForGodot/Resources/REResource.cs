namespace RGE;

using Godot;

[GlobalClass, Tool]
public partial class REResource : REObject
{
    [Export] public AssetReference? Asset { get; set; }
    [Export] public RESupportedFileFormats ResourceType { get; set; } = RESupportedFileFormats.Unknown;
}
