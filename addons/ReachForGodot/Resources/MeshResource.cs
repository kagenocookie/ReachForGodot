namespace RGE;

using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool]
public partial class MeshResource : REResourceProxy
{
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

