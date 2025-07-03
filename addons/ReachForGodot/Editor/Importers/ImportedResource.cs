using Godot;
using ReeLib;

namespace ReaGE;

/// <summary>
/// Proxy placeholder resource for resources that are imported directly from the godot filesystem
/// </summary>
[GlobalClass, Tool]
public partial class ImportedResource : REResource, IExportableAsset
{
    [Export] public Resource? Resource { get; set; }
    [Export] public KnownFileFormats FileFormat {
        get => _fileFormat;
        set => base.ResourceType = _fileFormat = value;
    }

    private KnownFileFormats _fileFormat;
}
