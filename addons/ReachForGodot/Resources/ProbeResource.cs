namespace ReaGE;

using Godot;

[GlobalClass, Tool, ResourceHolder("prb", SupportedFileFormats.Probe)]
public partial class ProbeResource : REResource
{
    public ProbeResource() : base(SupportedFileFormats.Probe)
    {
    }
}
