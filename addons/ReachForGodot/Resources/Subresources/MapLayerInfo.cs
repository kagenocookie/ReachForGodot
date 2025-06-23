namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class MapLayerInfo : Resource
{
    [Export] public string? Name { get; set; }
    [Export] public Color Color { get; set; }
    [Export(PropertyHint.Layers3DNavigation)] public uint Mask { get; set; }
}
