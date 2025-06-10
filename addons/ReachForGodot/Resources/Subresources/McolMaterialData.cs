namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class McolMaterialData : Resource
{
    [Export] public string? MainString { get; set; }
    [Export] public string? SubString { get; set; }
    [Export] public StandardMaterial3D? Material { get; set; }
}
