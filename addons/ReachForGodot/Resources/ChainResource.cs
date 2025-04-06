namespace ReaGE;

using Godot;

[GlobalClass, Tool, ResourceHolder("chain", SupportedFileFormats.Chain)]
public partial class ChainResource : REResource
{
    public ChainResource() : base(SupportedFileFormats.Chain)
    {
    }
}
