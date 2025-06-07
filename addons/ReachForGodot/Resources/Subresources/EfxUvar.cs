namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class EfxUvar : Resource
{
    [Export] public int type;
    [Export] public string? filepath;
    [Export] public string? group;
}