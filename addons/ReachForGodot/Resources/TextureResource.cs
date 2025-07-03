namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using ReeLib;

[GlobalClass, Tool, ResourceHolder("tex", KnownFileFormats.Texture)]
public partial class TextureResource : REResourceProxy
{
    public TextureResource() : base(KnownFileFormats.Texture)
    {
    }

    protected TextureResource(KnownFileFormats texformat) : base (texformat) { }

    protected override async Task<Resource?> Import()
    {
        if (Asset?.AssetFilename == null) return null;

        return await CreateImporter().Texture.ImportAssetGetResource(this);
    }
}
