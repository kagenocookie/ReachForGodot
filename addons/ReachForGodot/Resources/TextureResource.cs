namespace ReaGE;

using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool, ResourceHolder("tex", RESupportedFileFormats.Texture)]
public partial class TextureResource : REResourceProxy
{
    public TextureResource() : base(RESupportedFileFormats.Texture)
    {
    }

    protected TextureResource(RESupportedFileFormats texformat) : base (texformat) { }

    protected override async Task<Resource?> Import()
    {
        if (Asset?.AssetFilename == null) return null;

        return await CreateImporter().Texture.ImportAssetGetResource(this);
    }
}
