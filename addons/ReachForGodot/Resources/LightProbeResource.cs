namespace ReaGE;

using Godot;

[GlobalClass, Tool, ResourceHolder("lprb", SupportedFileFormats.LightProbe)]
public partial class LightProbeResource : REResource
{
    public LightProbeResource() : base(SupportedFileFormats.LightProbe)
    {
    }
}
