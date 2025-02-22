namespace RGE;

using System;
using System.Threading.Tasks;
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

    public override void _EnterTree()
    {
        ComponentContainer?.SetDisplayFolded(true);
    }

    public Task AddComponent(REComponent component)
    {
        return EnsureComponentContainerSetup().AddChildAsync(component, Owner);
    }

    public REComponent? GetComponent(string classname)
    {
        return ComponentContainer?.FindChildWhere<REComponent>(x => x is REComponent rec && rec.Classname == classname);
    }
}
