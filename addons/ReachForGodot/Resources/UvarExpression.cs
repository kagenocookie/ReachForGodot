namespace ReaGE;

using Godot;
using Godot.Collections;

[GlobalClass, Tool]
public partial class UvarExpression : Resource
{
    [Export] public UvarExpressionNode[] Nodes { get; set; } = System.Array.Empty<UvarExpressionNode>();
    [Export] public Godot.Collections.Array<Vector4I>? Connections { get; set; }
    [Export] public int OutputNodeId { get; set; }
    [Export] public int UnknownId { get; set; }
}
