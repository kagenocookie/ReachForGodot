namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class CollisionLayerDefinition : Resource
{
    [Export] public string? Name { get; set; }
    [Export] public string? guidString { get; set; }
    public Guid Guid
    {
        get => Guid.TryParse(guidString, out var guid) ? guid : Guid.Empty;
        set => guidString = value.ToString();
    }
    [Export] public Color Color { get; set; }
    [Export] public uint Value1 { get; set; }
    [Export] public uint Value2 { get; set; }
    [Export] public uint Value3 { get; set; }
    [Export] public uint Value4 { get; set; }
    [Export(PropertyHint.Layers3DPhysics)] public uint Bits1 { get; set; }
    [Export(PropertyHint.Layers3DPhysics)] public uint Bits2 { get; set; }
}
