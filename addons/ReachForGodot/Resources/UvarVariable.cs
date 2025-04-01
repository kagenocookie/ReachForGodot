namespace ReaGE;

using Godot;
using Godot.Collections;
using static RszTool.UvarFile;
using static RszTool.UvarFile.Variable;

[GlobalClass, Tool]
public partial class UvarVariable : Resource
{
    [Export] public SupportedGame Game { get; set; }
    [Export] public TypeKind Type { get; set; }
    [Export] public UvarFlags Flags { get; set; }
    [Export] public Variant Value { get; set; }
    [Export] public string GuidString { get; set; } = string.Empty;
    [Export] public UvarExpression? Expression { get; set; }

    public Guid Guid {
        get => Guid.Parse(GuidString);
        set => GuidString = value.ToString();
    }

    public override void _ValidateProperty(Dictionary property)
    {
        if (property["name"].AsStringName() == PropertyName.Value) {
            property["type"] = (int)UvarVarToVariantType(Type, Flags);
        }
        base._ValidateProperty(property);
    }

    public static object? VariantToUvar(Variant value, TypeKind type, RszTool.UvarFile.UvarFlags flags) => type switch {
        TypeKind.Enum => value.AsInt32(),
        TypeKind.Boolean => value.AsBool(),
        TypeKind.Int8 => ((flags & UvarFlags.IsVec3) != 0) ? value.AsGodotArray<sbyte>().ToArray() : (sbyte)value,
        TypeKind.Uint8 => ((flags & UvarFlags.IsVec3) != 0) ? (byte[])value : (byte)value,
        TypeKind.Int16 => ((flags & UvarFlags.IsVec3) != 0) ? value.AsGodotArray<short>().ToArray() : (short)value,
        TypeKind.Uint16 => ((flags & UvarFlags.IsVec3) != 0) ? value.AsGodotArray<ushort>().ToArray() : (ushort)value,
        TypeKind.Int32 => ((flags & UvarFlags.IsVec3) != 0) ? (int[])value : (int)value,
        TypeKind.Uint32 => ((flags & UvarFlags.IsVec3) != 0) ? value.AsGodotArray<uint>().ToArray() : (uint)value,
        TypeKind.Single => ((flags & UvarFlags.IsVec3) != 0) ? value.AsVector3().ToRsz() : (float)value,
        TypeKind.Double => value.AsDouble(),
        TypeKind.C8 => value.AsString(),
        TypeKind.C16 => value.AsString(),
        TypeKind.String => value.AsString(),
        TypeKind.Trigger => default,
        TypeKind.Vec2 => value.AsVector2().ToRsz(),
        TypeKind.Vec3 => value.AsVector3().ToRsz(),
        TypeKind.Vec4 => value.AsVector4().ToRsz(),
        TypeKind.Matrix => value.AsProjection().ToRsz(),
        TypeKind.GUID => Guid.TryParse(value.AsString(), out var guid) ? guid : Guid.Empty,
        _ => throw new Exception("Unhandled UVAR variable type " + type),
    };

    public static Variant UvarVarToVariant(object? value, TypeKind type, RszTool.UvarFile.UvarFlags flags) => value == null ? default : type switch {
        TypeKind.Enum => (int)value,
        TypeKind.Boolean => (bool)value,
        TypeKind.Int8 => ((flags & UvarFlags.IsVec3) != 0) ? new Vector3I(((sbyte[])value)[0], ((sbyte[])value)[1], ((sbyte[])value)[2]) : (sbyte)value,
        TypeKind.Uint8 => ((flags & UvarFlags.IsVec3) != 0) ? new Vector3I(((byte[])value)[0], ((byte[])value)[1], ((byte[])value)[2]) : (byte)value,
        TypeKind.Int16 => ((flags & UvarFlags.IsVec3) != 0) ? new Vector3I(((short[])value)[0], ((short[])value)[1], ((short[])value)[2]) : (short)value,
        TypeKind.Uint16 => ((flags & UvarFlags.IsVec3) != 0) ? new Vector3I(((ushort[])value)[0], ((ushort[])value)[1], ((ushort[])value)[2]) : (ushort)value,
        TypeKind.Int32 => ((flags & UvarFlags.IsVec3) != 0) ? (int[])value : (int)value,
        TypeKind.Uint32 => ((flags & UvarFlags.IsVec3) != 0) ? new Godot.Collections.Array<uint>((uint[])value) : (uint)value,
        TypeKind.Single => ((flags & UvarFlags.IsVec3) != 0) ? ((System.Numerics.Vector3)value).ToGodot() : (float)value,
        TypeKind.Double => (double)value,
        TypeKind.C8 => (string)value,
        TypeKind.C16 => (string)value,
        TypeKind.String => (string)value,
        TypeKind.Trigger => default,
        TypeKind.Vec2 => (Vector2)value,
        TypeKind.Vec3 => (Vector3)value,
        TypeKind.Vec4 => (Vector4)value,
        TypeKind.Matrix => ((RszTool.via.mat4)value).ToProjection(),
        TypeKind.GUID => ((Guid)value).ToString(),
        _ => throw new Exception("Unhandled UVAR variable type " + type),
    };

    public static Variant.Type UvarVarToVariantType(TypeKind type, RszTool.UvarFile.UvarFlags flags) => type switch {
        TypeKind.Enum => Variant.Type.Int,
        TypeKind.Boolean => Variant.Type.Bool,
        TypeKind.Int8 => ((flags & UvarFlags.IsVec3) != 0) ? Variant.Type.Vector3I : Variant.Type.Int,
        TypeKind.Uint8 => ((flags & UvarFlags.IsVec3) != 0) ? Variant.Type.Vector3I : Variant.Type.Int,
        TypeKind.Int16 => ((flags & UvarFlags.IsVec3) != 0) ? Variant.Type.Vector3I : Variant.Type.Int,
        TypeKind.Uint16 => ((flags & UvarFlags.IsVec3) != 0) ? Variant.Type.Vector3I : Variant.Type.Int,
        TypeKind.Int32 => ((flags & UvarFlags.IsVec3) != 0) ? Variant.Type.PackedInt32Array : Variant.Type.Int,
        TypeKind.Uint32 => ((flags & UvarFlags.IsVec3) != 0) ? Variant.Type.Array : Variant.Type.Int,
        TypeKind.Single => ((flags & UvarFlags.IsVec3) != 0) ? Variant.Type.Vector3 : Variant.Type.Float,
        TypeKind.Double => Variant.Type.Float,
        TypeKind.C8 => Variant.Type.String,
        TypeKind.C16 => Variant.Type.String,
        TypeKind.String => Variant.Type.String,
        TypeKind.Trigger => Variant.Type.Nil,
        TypeKind.Vec2 => Variant.Type.Vector2,
        TypeKind.Vec3 => Variant.Type.Vector3,
        TypeKind.Vec4 => Variant.Type.Vector4,
        TypeKind.Matrix => Variant.Type.Projection,
        TypeKind.GUID => Variant.Type.String,
        _ => throw new Exception("Unhandled UVAR variable type " + type),
    };
}
