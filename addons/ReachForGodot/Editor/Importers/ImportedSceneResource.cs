using Godot;

namespace ReaGE;

/// <summary>
/// Proxy placeholder resource for scene resources imported directly from the godot filesystem
/// </summary>
[GlobalClass, Tool]
public partial class ImportedSceneResource : ImportedResource
{
    [Export] public new REResource? Resource
    {
        get => (REResource?)base.Resource;
        set => base.Resource = value;
    }

    private SupportedFileFormats _fileFormat;
}
