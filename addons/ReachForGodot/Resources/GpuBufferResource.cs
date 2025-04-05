namespace ReaGE;

using Godot;

[GlobalClass, Tool, ResourceHolder("gpbf", SupportedFileFormats.GpuBuffer)]
public partial class GpuBufferResource : REResource
{
    public GpuBufferResource() : base(SupportedFileFormats.GpuBuffer)
    {
    }
}
