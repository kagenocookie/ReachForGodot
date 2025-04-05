namespace ReaGE;

using Godot;
using Godot.Collections;

[GlobalClass, Tool, ResourceHolder("uvar", SupportedFileFormats.Uvar)]
public partial class UvarResource : REResource, IImportableAsset, IExportableAsset
{
    [Export] public string? OriginalName { get; set; }
    [Export] public UvarResource[]? EmbeddedData { get; set; }
    [Export] public Array<UvarVariable>? Variables { get; set; }

    private Array<Dictionary>? propertyList;

    public UvarResource() : base(SupportedFileFormats.Uvar)
    {
    }

    public bool IsEmpty => (Variables == null || Variables.Count == 0) && (EmbeddedData == null || EmbeddedData.Length == 0);
}
