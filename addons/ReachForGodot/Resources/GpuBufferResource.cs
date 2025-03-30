namespace ReaGE;

using Godot;

[GlobalClass, Tool, ResourceHolder("gpbf", RESupportedFileFormats.GpuBuffer)]
public partial class GpuBufferResource : REResource
{
    public GpuBufferResource() : base(RESupportedFileFormats.GpuBuffer)
    {
    }
}
