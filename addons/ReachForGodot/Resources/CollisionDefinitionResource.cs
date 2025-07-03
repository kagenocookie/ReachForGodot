namespace ReaGE;

using Godot;
using ReeLib;

[GlobalClass, Tool, ResourceHolder("cdef", KnownFileFormats.CollisionDefinition)]
public partial class CollisionDefinitionResource : REResource, IImportableAsset
{
    public CollisionDefinitionResource() : base(KnownFileFormats.CollisionDefinition)
    {
    }

    [Export] public CollisionLayerDefinition[]? Layers { get; set; }
    [Export] public CollisonMaskDefinition[]? Masks { get; set; }
    [Export] public CollisionMaterialDefinition[]? Materials { get; set; }
    [Export] public CollisionAttributeDefinition[]? Attributes { get; set; }
    [Export] public CollisionPresetDefinition[]? Presets { get; set; }

    public bool IsEmpty => !(Layers?.Length > 0);
}
