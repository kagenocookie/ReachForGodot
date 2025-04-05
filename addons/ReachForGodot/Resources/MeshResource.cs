namespace ReaGE;

using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool, ResourceHolder("mesh", SupportedFileFormats.Mesh)]
public partial class MeshResource : REResourceProxy, IImportableAsset
{
    public PackedScene? ImportedMesh => ImportedResource as PackedScene;

    public MeshResource() : base(SupportedFileFormats.Mesh)
    {
    }

    IEnumerable<(string label, GodotImportOptions importMode)> IImportableAsset.SupportedImportTypes => [
        ("Reimport", GodotImportOptions.fullReimport),
        ("Reimport (textured)", GodotImportOptions.fullReimportTextured),
        ("Reimport (untextured)", GodotImportOptions.fullReimportUntextured)
    ];

    protected override async Task<Resource?> Import()
    {
        if (Asset?.AssetFilename == null) return null;

        return await CreateImporter().Mesh.ImportAssetGetResource(this);
    }
}
