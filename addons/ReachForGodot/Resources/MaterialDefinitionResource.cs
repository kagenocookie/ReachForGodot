namespace ReaGE;

using Godot;

[GlobalClass, Tool, ResourceHolder("mdf2", RESupportedFileFormats.MaterialDefinition)]
public partial class MaterialDefinitionResource : REResource, IImportableAsset, IExportableAsset
{
    [Export] public MaterialResource[]? Materials { get; set; }

    public MaterialDefinitionResource() : base(RESupportedFileFormats.MaterialDefinition)
    {
    }

    public bool IsEmpty => Materials == null || Materials.Length == 0;
}
