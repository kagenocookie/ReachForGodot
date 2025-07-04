namespace ReaGE;

using Godot;
using ReeLib;

[GlobalClass, Tool, ResourceHolder("cmat", KnownFileFormats.CollisionMaterial)]
public partial class CollisionMaterialResource : REResource, IImportableAsset
{
    [Export] public string? material;
    [Export] public string[]? attributes;

    public Guid MaterialGuid
    {
        get => Guid.TryParse(material, out var guid) ? guid : Guid.Empty;
        set => material = value.ToString();
    }
    public Guid[] AttributeGuids
    {
        get => attributes?.Select(attr => Guid.TryParse(attr, out var guid) ? guid : Guid.Empty).ToArray() ?? Array.Empty<Guid>();
        set => attributes = value.Select(guid => guid.ToString()).ToArray();
    }

    public CollisionMaterialResource() : base(KnownFileFormats.CollisionMaterial)
    {
    }

    public bool IsEmpty => string.IsNullOrEmpty(material);
}
