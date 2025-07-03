namespace ReaGE;

using Godot;
using ReeLib;

[GlobalClass, Tool, ResourceHolder("chain", KnownFileFormats.Chain)]
public partial class ChainResource : REResource
{
    public ChainResource() : base(KnownFileFormats.Chain)
    {
    }
}
