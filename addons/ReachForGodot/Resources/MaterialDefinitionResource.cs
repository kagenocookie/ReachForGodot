namespace ReaGE;

using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool, ResourceHolder("mdf2", RESupportedFileFormats.MaterialDefinition)]
public partial class MaterialDefinitionResource : REResource, IImportableAsset, IExportableAsset
{
    [Export] public MaterialResource[]? Materials { get; set; }

    public MaterialDefinitionResource() : base(RESupportedFileFormats.MaterialDefinition)
    {
    }

    public bool IsEmpty => Materials == null || Materials.Length == 0;

    public async Task EnsureNotEmpty()
    {
        if (!IsEmpty) return;

        await CreateImporter(GodotImportOptions.importMissing).Mdf2.ImportFromFile(this);
    }
}
