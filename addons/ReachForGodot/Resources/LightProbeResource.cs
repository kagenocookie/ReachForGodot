namespace ReaGE;

using Godot;
using ReeLib;

[GlobalClass, Tool, ResourceHolder("lprb", KnownFileFormats.LightProbes)]
public partial class LightProbeResource : REResource
{
    public LightProbeResource() : base(KnownFileFormats.LightProbes)
    {
    }
}
