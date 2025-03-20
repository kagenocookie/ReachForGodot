namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class RequestSetCollider : Node
{
    [Export] public uint ID { get; set; }
    [Export] public string? OriginalName { get; set; }
    [Export] public string? KeyName { get; set; }
    [Export] public RequestSetCollisionGroup? Group { get; set; }
    [Export] public REObject? Data { get; set; }
}
