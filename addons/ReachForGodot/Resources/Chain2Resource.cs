namespace ReaGE;

using Godot;

[GlobalClass, Tool, ResourceHolder("chain2", SupportedFileFormats.Chain2)]
public partial class Chain2Resource : REResource
{
    public Chain2Resource() : base(SupportedFileFormats.Chain2)
    {
    }
}
