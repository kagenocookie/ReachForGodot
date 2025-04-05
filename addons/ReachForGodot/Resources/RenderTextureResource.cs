namespace ReaGE;

using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool, ResourceHolder("rtex", SupportedFileFormats.RenderTexture)]
public partial class RenderTextureResource : TextureResource
{
    public RenderTextureResource() : base(SupportedFileFormats.RenderTexture)
    {
    }

    protected override Task<Resource?> Import()
    {
        return Task.FromResult<Resource?>(null);
    }
}
