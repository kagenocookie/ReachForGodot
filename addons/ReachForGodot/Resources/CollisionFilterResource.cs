namespace ReaGE;

using Godot;
using ReeLib;

[GlobalClass, Tool, ResourceHolder("cfil", KnownFileFormats.CollisionFilter)]
public partial class CollisionFilterResource : REResource, IImportableAsset
{
    [Export] public string? Layer;
    [Export] public string[]? Masks;

    public Guid LayerGuid
    {
        get => Guid.TryParse(Layer, out var guid) ? guid : Guid.Empty;
        set => Layer = value.ToString();
    }
    public Guid[] MaskGuids
    {
        get => Masks?.Select(attr => Guid.TryParse(attr, out var guid) ? guid : Guid.Empty).ToArray() ?? Array.Empty<Guid>();
        set => Masks = value.Select(guid => guid.ToString()).ToArray();
    }

    public CollisionFilterResource() : base(KnownFileFormats.CollisionFilter)
    {
    }

    public bool IsEmpty => string.IsNullOrEmpty(Layer) || Masks == null;
}
