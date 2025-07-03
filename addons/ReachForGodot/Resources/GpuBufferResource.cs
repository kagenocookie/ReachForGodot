namespace ReaGE;

using Godot;
using ReeLib;

[GlobalClass, Tool, ResourceHolder("gpbf", KnownFileFormats.ByteBuffer)]
public partial class GpuBufferResource : REResource
{
    public GpuBufferResource() : base(KnownFileFormats.ByteBuffer)
    {
    }
}
