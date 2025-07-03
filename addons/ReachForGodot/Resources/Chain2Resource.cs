namespace ReaGE;

using Godot;
using ReeLib;

[GlobalClass, Tool, ResourceHolder("chain2", KnownFileFormats.Chain2)]
public partial class Chain2Resource : REResource
{
    public Chain2Resource() : base(KnownFileFormats.Chain2)
    {
    }
}
