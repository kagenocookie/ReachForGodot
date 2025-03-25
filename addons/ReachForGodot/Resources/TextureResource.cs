namespace ReaGE;

using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool, ResourceHolder("tex", RESupportedFileFormats.Texture)]
public partial class TextureResource : REResourceProxy
{
    public TextureResource() : base(RESupportedFileFormats.Texture)
    {
    }

    protected override async Task<Resource?> Import()
    {
        if (Asset?.AssetFilename == null) return null;

        ImportedResource = await AsyncImporter.QueueAssetImport(Asset.AssetFilename, Game);
        if (!string.IsNullOrEmpty(ResourcePath)) {
            ResourceSaver.Save(this);
        }
        return ImportedResource;
    }
}
