namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class CollisionPresetDefinition : Resource
{
    [Export] public string? Name { get; set; }
    [Export] public string? Description { get; set; }
    [Export] public string? guidString { get; set; }
    public Guid Guid
    {
        get => Guid.TryParse(guidString, out var guid) ? guid : Guid.Empty;
        set => guidString = value.ToString();
    }

    [Export] public uint MaskBits { get; set; }
    [Export] public Color Color { get; set; }
    [Export] public int Value1 { get; set; }
    [Export] public int Value2 { get; set; }
    [Export] public uint Value3 { get; set; }
}
