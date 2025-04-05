namespace ReaGE;

using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool, ResourceHolder("tex", SupportedFileFormats.Texture)]
public partial class TextureResource : REResourceProxy
{
    public TextureResource() : base(SupportedFileFormats.Texture)
    {
    }

    protected TextureResource(SupportedFileFormats texformat) : base (texformat) { }

    protected override async Task<Resource?> Import()
    {
        if (Asset?.AssetFilename == null) return null;

        return await CreateImporter().Texture.ImportAssetGetResource(this);
    }
}
