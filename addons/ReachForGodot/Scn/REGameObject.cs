namespace RFG;

using System;
using Godot;
using RszTool;

[GlobalClass, Tool]
public partial class REGameObject : Node3D
{
    [Export] public int ObjectId { get; set; } = -1;
    [Export] public bool Enabled { get; set; } = true;
    [Export] public string Uuid { get; set; } = "00000000-0000-0000-0000-000000000000";
    [Export] public string? Prefab { get; set; }
    [Export] public Node? ComponentContainer { get; set; }

    public SceneFolder? SceneRoot => this.FindNodeInParents<SceneFolder>();

    public Node EnsureComponentContainerSetup()
    {
        if (ComponentContainer == null) {
            AddChild(ComponentContainer = new Node() { Name = "Components" });
            ComponentContainer.Owner = Owner;
            MoveChild(ComponentContainer, 0);
        }

        return ComponentContainer;
    }

    public void AddComponent(REComponent component)
    {
        EnsureComponentContainerSetup().AddOwnedChild(component);
    }

    public REComponent? GetComponent(string classname)
    {
        var child = ComponentContainer?.FindChildWhere<Node>(x => x is REComponent rec && rec.Classname == classname || x.FindChild("ComponentInfo") is REComponent);
        return child as REComponent ?? child?.FindChild("ComponentInfo") as REComponent;
    }
}
