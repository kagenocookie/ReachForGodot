namespace RFG;

using System;
using Godot;
using RszTool;

[GlobalClass, Tool]
public partial class REGameObject : Node
{
    [Export] public int ObjectId = -1;
    [Export] public bool Enabled { get; set; } = true;
    [Export] public string Uuid { get; set; } = "00000000-0000-0000-0000-000000000000";
    [Export] public string? Prefab { get; set; }
    [Export] public Node? ChildContainer { get; set; }
    [Export] public Node3D? Root3D { get; set; }

    public Node EnsureChildContainerSetup()
    {
        if (ChildContainer == null) {
            AddChild(ChildContainer = new Node() { Name = "Children" });
            ChildContainer.Owner = Owner;
            MoveChild(ChildContainer, 0);
        }

        return ChildContainer;
    }
}
