namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class UvarExpressionNode : Resource
{
    [Export] public string NodeType { get; set; } = string.Empty;
    [Export] public int UnknownNumber { get; set; }

    [Export] public UvarExpressionNodeParameter[]? Parameters { get; set; }
}
