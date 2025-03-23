namespace ReaGE;

using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool]
public partial class CollisionFilterResource : REResource
{
    [Export] public string? Uuid;
    [Export] public string[]? CollisionGuids;
}

