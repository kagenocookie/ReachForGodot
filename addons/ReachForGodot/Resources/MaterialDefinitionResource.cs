namespace ReaGE;

using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool, ResourceHolder("mdf2", SupportedFileFormats.MaterialDefinition)]
public partial class MaterialDefinitionResource : REResource, IImportableAsset, IExportableAsset
{
    [Export] public MaterialResource[]? Materials { get; set; }

    public MaterialDefinitionResource() : base(SupportedFileFormats.MaterialDefinition)
    {
    }

    public bool IsEmpty => Materials == null || Materials.Length == 0;

    public async Task EnsureNotEmpty()
    {
        if (!IsEmpty) return;

        await CreateImporter(GodotImportOptions.importMissing).Mdf2.ImportFromFile(this);
    }
}
