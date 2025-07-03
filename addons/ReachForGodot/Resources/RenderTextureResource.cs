namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using ReeLib;

[GlobalClass, Tool, ResourceHolder("rtex", KnownFileFormats.RenderTexture)]
public partial class RenderTextureResource : TextureResource
{
    public RenderTextureResource() : base(KnownFileFormats.RenderTexture)
    {
    }

    protected override Task<Resource?> Import()
    {
        return Task.FromResult<Resource?>(null);
    }
}
