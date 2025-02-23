namespace RGE;

using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool]
public partial class MeshResource : REResourceProxy
{
    protected override async Task<Resource?> Import()
    {
        if (Asset?.AssetFilename == null) return null;

        return ImportedResource = await AsyncImporter.QueueAssetImport(Asset.AssetFilename, Game);
    }
}

