namespace ReaGE;

using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool, ResourceHolder("mesh", RESupportedFileFormats.Mesh)]
public partial class MeshResource : REResourceProxy
{
    public PackedScene? ImportedMesh => ImportedResource as PackedScene;

    public MeshResource() : base(RESupportedFileFormats.Mesh)
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
