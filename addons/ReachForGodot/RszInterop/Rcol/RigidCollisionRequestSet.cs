namespace ReaGE;

using System;
using Godot;

[GlobalClass, Tool]
public partial class RigidCollisionRequestSet : Node
{
    [Export] public uint ID { get; set; }
    [Export] public string? OriginalName { get; set; }
    [Export] public string? KeyName { get; set; }
    [Export] public RigidCollisionGroup? Group { get; set; }
    [Export] public REObject? Data { get; set; }
}
