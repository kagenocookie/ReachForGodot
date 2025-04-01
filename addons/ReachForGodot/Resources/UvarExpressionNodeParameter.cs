namespace ReaGE;

using Godot;
using Godot.Collections;
using static RszTool.UvarFile;
using static RszTool.UvarFile.UvarExpression;

[GlobalClass, Tool]
public partial class UvarExpressionNodeParameter : Resource
{
    [Export] public NodeValueType ValueType { get; set; }
    [Export] public Variant Value { get; set; }
    [Export] public uint SlotNameHash { get; set; }

    public override void _ValidateProperty(Dictionary property)
    {
        if (property["name"].AsStringName() == PropertyName.Value) {
            property["type"] = (int)NodeVarToVariantType(ValueType);
        }
        base._ValidateProperty(property);
    }

    public static Variant NodeVarToVariant(object? value, NodeValueType type) => value == null ? default : type switch {
        NodeValueType.UInt32Maybe => (uint)value,
        NodeValueType.Int32 => (int)value,
        NodeValueType.Single => (float)value,
        NodeValueType.Guid => value.ToString()!,
        _ => default,
    };

    public static Variant.Type NodeVarToVariantType(NodeValueType type) => type switch {
        NodeValueType.UInt32Maybe => Variant.Type.Int,
        NodeValueType.Int32 => Variant.Type.Int,
        NodeValueType.Single => Variant.Type.Float,
        NodeValueType.Guid => Variant.Type.String,
        _ => default,
    };

    public static object? VariantToNodeVar(Variant value, NodeValueType type) => type switch {
        NodeValueType.UInt32Maybe => value.AsUInt32(),
        NodeValueType.Int32 => value.AsInt32(),
        NodeValueType.Single => value.AsSingle(),
        NodeValueType.Guid => Guid.TryParse(value.AsString(), out var guid) ? guid : Guid.Empty,
        _ => default,
    };
}
