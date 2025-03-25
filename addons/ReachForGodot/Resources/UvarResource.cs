namespace ReaGE;

using Godot;

[GlobalClass, Tool, ResourceHolder("uvar", RESupportedFileFormats.Uvar)]
public partial class UvarResource : REResource
{
    public UvarResource() : base(RESupportedFileFormats.Uvar)
    {
    }
}
