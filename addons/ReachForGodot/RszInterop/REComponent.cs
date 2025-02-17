namespace RFG;

using System;
using Godot;
using RszTool;

[GlobalClass, Tool]
public abstract partial class REComponent : Node
{
    [Export] public int ObjectId = -1;
    [Export] public bool Enabled { get; set; } = true;
    [Export] public string? Classname { get; set; }

    public REGameObject? GameObject => this.FindNodeInParents<REGameObject>();
    public abstract void Setup(IRszContainerNode root, REGameObject gameObject, RszInstance rsz);
}
