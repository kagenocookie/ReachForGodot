namespace ReaGE;

using Godot;
using Godot.Collections;

[GlobalClass, Tool]
public partial class UvarExpressionNode : Resource
{
    [Export] public string NodeType { get; set; } = string.Empty;
    [Export] public int UnknownNumber { get; set; }

    [Export] public UvarExpressionNodeParameter[]? Parameters { get; set; }
}
