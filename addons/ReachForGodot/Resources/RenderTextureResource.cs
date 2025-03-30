namespace ReaGE;

using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool, ResourceHolder("rtex", RESupportedFileFormats.RenderTexture)]
public partial class RenderTextureResource : REResourceProxy
{
    public RenderTextureResource() : base(RESupportedFileFormats.RenderTexture)
    {
    }

    protected override Task<Resource?> Import()
    {
        return Task.FromResult<Resource?>(null);
    }
}
