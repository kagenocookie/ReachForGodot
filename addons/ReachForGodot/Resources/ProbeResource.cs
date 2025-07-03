namespace ReaGE;

using Godot;
using ReeLib;

[GlobalClass, Tool, ResourceHolder("prb", KnownFileFormats.Probes)]
public partial class ProbeResource : REResource
{
    public ProbeResource() : base(KnownFileFormats.Probes)
    {
    }
}
