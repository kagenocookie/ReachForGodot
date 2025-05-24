namespace ReaGE;

using Godot;
using Godot.Collections;
using RszTool.Efx;

[GlobalClass, Tool]
public partial class EfxExpressionParameter : Resource
{
    private EfxExpressionParameterType _parameterType;
    [Export]
    public EfxExpressionParameterType ParameterType {
        get => _parameterType;
        set {
            _parameterType = value;
            NotifyPropertyListChanged();
        }
    }

    [Export] private Vector3I _value;

    public Variant Value => _parameterType switch {
        EfxExpressionParameterType.Float => BitConverter.Int32BitsToSingle(_value.X),
        EfxExpressionParameterType.Color => new Color((uint)_value.X),
        EfxExpressionParameterType.Range => RawValueF,
        EfxExpressionParameterType.Float2 => BitConverter.Int32BitsToSingle(_value.X),
        _ => _value,
    };
    public Vector3I RawValue { get => _value; set => _value = value; }
    public Vector3 RawValueF {
        get => new Vector3(BitConverter.Int32BitsToSingle(_value.X), BitConverter.Int32BitsToSingle(_value.Y), BitConverter.Int32BitsToSingle(_value.Z));
        set => _value = new Vector3I(BitConverter.SingleToInt32Bits(value.X), BitConverter.SingleToInt32Bits(value.Y), BitConverter.SingleToInt32Bits(value.Z));
    }

    [Export] public string? originalName;

    public override void _ValidateProperty(Dictionary property)
    {
        var name = property["name"].AsStringName();
        if (name == PropertyName._value) {
            property["usage"] = (uint)(PropertyUsageFlags.Storage);
        }
        base._ValidateProperty(property);
    }

    public override Array<Dictionary> _GetPropertyList()
    {
        return _parameterType switch {
            EfxExpressionParameterType.Float => PropertyListFloat,
            EfxExpressionParameterType.Float2 => PropertyListFloat,
            EfxExpressionParameterType.Color => PropertyListColor,
            EfxExpressionParameterType.Range => PropertyListRange,
            _ => base._GetPropertyList(),
        };
    }

    private static readonly StringName ValueProperty = "value";
    private static Array<Dictionary> PropertyListFloat = [new Dictionary() {
        ["type"] = (uint)Variant.Type.Float,
        ["usage"] = (uint)PropertyUsageFlags.Editor,
        ["name"] = ValueProperty,
    }];
    private static Array<Dictionary> PropertyListColor = [new Dictionary() {
        ["type"] = (uint)Variant.Type.Color,
        ["usage"] = (uint)PropertyUsageFlags.Editor,
        ["name"] = ValueProperty,
    }];
    private static Array<Dictionary> PropertyListRange = [new Dictionary() {
        ["type"] = (uint)Variant.Type.Vector3,
        ["usage"] = (uint)PropertyUsageFlags.Editor,
        ["name"] = ValueProperty,
    }];

    public override Variant _Get(StringName property)
    {
        if (property == ValueProperty) {
            return Value;
        }
        return base._Get(property);
    }

    public override bool _Set(StringName property, Variant value)
    {
        if (property == ValueProperty) {
            if (_parameterType == EfxExpressionParameterType.Color) {
                _value = new Vector3I((int)value.AsColor().ToRgba32(), 0, 0);
            } else if (_parameterType == EfxExpressionParameterType.Range) {
                RawValueF = value.AsVector3();
            } else {
                _value.X = BitConverter.SingleToInt32Bits(value.AsSingle());
            }
            return true;
        }
        return base._Set(property, value);
    }
}