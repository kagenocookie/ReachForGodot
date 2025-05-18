namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class EfxCollisionEffect : Resource
{
    [Export] public string? OriginalName;
    [Export] public Godot.Collections.Array<uint>? Values;
}