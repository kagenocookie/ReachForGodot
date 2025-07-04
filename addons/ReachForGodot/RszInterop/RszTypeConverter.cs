namespace ReaGE;

using System;
using System.Globalization;
using Godot;
using ReeLib;
using ReeLib.via;

public static class RszTypeConverter
{
    private static Type baseList = typeof(List<>);

    public static Variant FromRszValue(REField field, object value, SupportedGame game)
    {
        try {
            return FromRszValueUnsafe(field, value, game);
        } catch (NotSupportedException exception) {
            GD.PrintErr("Could not deserialize rsz value of type " + field.RszField.original_type + ":\n" + exception);
            return new Variant();
        } catch (RszRetryOpenException retryException) {
            retryException.LogRszRetryException();
            return new Variant();
        }
    }

    private static Variant FromRszValueUnsafe(REField field, object value, SupportedGame game)
    {
        if (field.RszField.array) {
            var newArray = new Godot.Collections.Array();
            if (value == null) return newArray;

            var type = value.GetType();
            if (type.IsArray) {
                foreach (var item in (object[])value) {
                    newArray.Add(FromRszValueSingleValue(field.RszField.type, item, game, field.RszField.original_type));
                }
            } else if (type.IsGenericType && type.GetGenericTypeDefinition() == baseList) {
                foreach (var item in ((IList<object>)value)) {
                    newArray.Add(FromRszValueSingleValue(field.RszField.type, item, game, field.RszField.original_type));
                }
            } else {
                GD.Print("Unhandled array type " + type.FullName);
            }
            return newArray;
        }

        return FromRszValueSingleValue(field.RszField.type, value, game, field.RszField.original_type);
    }

    public static uint SwapEndianness(uint value)
    {
        return
            ((value & 0xff000000) >> 24) +
            ((value & 0xff0000) >> 8) +
            ((value & 0xff00) << 8) +
            ((value & 0xff) << 24);
    }

    public static float SwapEndianness(float value)
    {
        return BitConverter.UInt32BitsToSingle(SwapEndianness(BitConverter.SingleToUInt32Bits(value)));
    }

    public static Variant FromRszValueSingleValue(RszFieldType type, object value, SupportedGame game, string? originalType)
    {
        switch (type) {
            case RszFieldType.Resource:
                if (value == null || value is string str && string.IsNullOrEmpty(str)) {
                    return new Variant();
                }
                break;
            case RszFieldType.UserData:
            case RszFieldType.Object:
                GD.PrintErr("Fields of type " + type + " shouldn't be handled from this method!");
                return new Variant();

            case RszFieldType.Sfix:
                return ((sfix)value).v;
            case RszFieldType.Sfix2:
                return new Vector2I(
                    ((Sfix2)value).x.v,
                    ((Sfix2)value).y.v
                );
            case RszFieldType.Sfix3:
                return new Vector3I(
                    ((Sfix3)value).x.v,
                    ((Sfix3)value).y.v,
                    ((Sfix3)value).z.v
                );
            case RszFieldType.Sfix4:
                return new Vector4I(
                    ((Sfix4)value).x.v,
                    ((Sfix4)value).y.v,
                    ((Sfix4)value).z.v,
                    ((Sfix4)value).w.v
                );
            case RszFieldType.Int2:
                return new Vector2I(
                    ((Int2)value).x,
                    ((Int2)value).y
                );
            case RszFieldType.Uint2:
                return new Vector2I(
                    (int)((Uint2)value).x,
                    (int)((Uint2)value).y
                );
            case RszFieldType.Int3:
                return new Vector3I(
                    ((Int3)value).x,
                    ((Int3)value).y,
                    ((Int3)value).z
                );
            case RszFieldType.Uint3:
                return new Vector3I(
                    (int)((Uint3)value).x,
                    (int)((Uint3)value).y,
                    (int)((Uint3)value).z
                );
            case RszFieldType.Int4:
                return new Vector4I(
                    ((Int4)value).x,
                    ((Int4)value).y,
                    ((Int4)value).z,
                    ((Int4)value).w
                );
            case RszFieldType.Uint4:
                return new Vector4I(
                    (int)((Uint4)value).x,
                    (int)((Uint4)value).y,
                    (int)((Uint4)value).z,
                    (int)((Uint4)value).w
                );
            case RszFieldType.S64:
                return (long)value;
            case RszFieldType.U64:
                return (ulong)value;
            case RszFieldType.Data:
                return (byte[])value;
            case RszFieldType.S8:
            case RszFieldType.S16:
            case RszFieldType.S32:
            case RszFieldType.U8:
            case RszFieldType.U16:
            case RszFieldType.Enum:
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            case RszFieldType.U32:
                return Convert.ToUInt32(value, CultureInfo.InvariantCulture);
            case RszFieldType.F16:
            case RszFieldType.F32:
            case RszFieldType.F64:
                return Convert.ToSingle(value, CultureInfo.InvariantCulture);
            case RszFieldType.RuntimeType:
            case RszFieldType.String:
                return (value as string)!;
            case RszFieldType.Bool:
                return (bool)value;
            case RszFieldType.Vec2:
            case RszFieldType.Float2:
            case RszFieldType.Point:
                return ((System.Numerics.Vector2)value).ToGodot();
            case RszFieldType.Size:
                return new Vector2(((ReeLib.via.Size)value).w, ((ReeLib.via.Size)value).h);
            case RszFieldType.Rect:
                return ((ReeLib.via.Rect)value).ToGodot();
            case RszFieldType.Range:
                return ((System.Numerics.Vector2)((ReeLib.via.Range)value)).ToGodot();
            case RszFieldType.RangeI:
                return new Vector2I(((ReeLib.via.RangeI)value).r, ((ReeLib.via.RangeI)value).s);
            case RszFieldType.Vec3:
            case RszFieldType.Float3:
                return ((System.Numerics.Vector3)value).ToGodot();
            case RszFieldType.Position:
                return ((ReeLib.via.Position)value).ToGodot();
            case RszFieldType.Vec4:
            case RszFieldType.Float4:
                return ((System.Numerics.Vector4)value).ToGodot();
            case RszFieldType.Quaternion:
                return ((System.Numerics.Quaternion)value).ToGodot();
            case RszFieldType.Color:
                return ((ReeLib.via.Color)value).ToGodot();
            case RszFieldType.Guid:
                return ((Guid)value).ToString();
            case RszFieldType.Uri:
                return originalType?.Contains("via.GameObjectRef") == true ? new GameObjectRef((Guid)value) : ((Guid)value).ToString();
            case RszFieldType.GameObjectRef:
                return new GameObjectRef((Guid)value);
            case RszFieldType.AABB:
                return ((ReeLib.via.AABB)value).ToGodot();
            case RszFieldType.Mat4:
                return ((ReeLib.via.mat4)value).ToProjection();
            case RszFieldType.OBB:
                return OrientedBoundingBox.FromRsz(((ReeLib.via.OBB)value));
            case RszFieldType.Sphere:
                return ((ReeLib.via.Sphere)value).ToVector4();
            case RszFieldType.LineSegment:
                return (LineSegment)((ReeLib.via.LineSegment)value);
            case RszFieldType.Plane:
                return (Plane)((ReeLib.via.Plane)value);
            case RszFieldType.PlaneXZ:
                return ((ReeLib.via.PlaneXZ)value).dist;
            case RszFieldType.Ray:
                return (Ray)((ReeLib.via.Ray)value);
            case RszFieldType.RayY:
                return (RayY)((ReeLib.via.RayY)value);
            case RszFieldType.Triangle:
                return (Triangle)((ReeLib.via.Triangle)value);
            case RszFieldType.Cylinder:
                return (Cylinder)((ReeLib.via.Cylinder)value);
            case RszFieldType.Ellipsoid:
                return (Ellipsoid)((ReeLib.via.Ellipsoid)value);
            case RszFieldType.Frustum:
                return (Frustum)((ReeLib.via.Frustum)value);
            case RszFieldType.KeyFrame:
                return (KeyFrame)((ReeLib.via.KeyFrame)value);
            case RszFieldType.Rect3D:
                return (Rect3D)((ReeLib.via.Rect3D)value);
            case RszFieldType.Capsule:
                return (Capsule)((ReeLib.via.Capsule)value);
            case RszFieldType.Area:
                return (Area)((ReeLib.via.Area)value);
            case RszFieldType.TaperedCapsule:
                return (TaperedCapsule)((ReeLib.via.TaperedCapsule)value);
            case RszFieldType.Cone:
                return (Cone)((ReeLib.via.Cone)value);
            case RszFieldType.Line:
                return (Line)((ReeLib.via.Line)value);
            case RszFieldType.Segment:
                return (Segment)((ReeLib.via.Segment)value);
        }

        GD.PrintErr("Unhandled conversion for rsz type " + type + " with value type " + value.GetType().FullName);
        return new Variant();
    }

    public static object? ToRszStruct(this Variant variant, REField field, SupportedGame game)
    {
        return ToRszStruct(variant, field.RszField.type, field.RszField.array, game);
    }

    public static object? ToRszStruct(this Variant variant, RszFieldType valueType, bool array, SupportedGame game)
    {

        if (array) {
            return valueType switch {
                RszFieldType.S16 => ConvertArray(variant.AsGodotArray<uint>(), static v => v),
                RszFieldType.U16 => ConvertArray(variant.AsGodotArray<uint>(), static v => v),
                RszFieldType.S32 or RszFieldType.Enum => ConvertArray(variant.AsInt32Array()),
                RszFieldType.U32 => ConvertArray(variant.AsGodotArray<uint>(), static v => v),
                RszFieldType.S64 => ConvertArray(variant.AsInt64Array()),
                RszFieldType.U64 => ConvertArray(variant.AsGodotArray<uint>(), static v => v),
                RszFieldType.F32 => ConvertArray(variant.AsFloat32Array()),
                RszFieldType.F64 => ConvertArray(variant.AsFloat64Array()),
                RszFieldType.S8 => ConvertArray(variant.AsGodotArray<uint>(), static v => v),
                RszFieldType.U8 => ConvertArray(variant.AsByteArray()),
                RszFieldType.String => ConvertArray(variant.AsStringArray()),
                RszFieldType.RuntimeType => ConvertArray(variant.AsStringArray()),
                RszFieldType.Color => ConvertArray(variant.AsColorArray(), static (val) => val.ToRsz()),
                RszFieldType.Vec2 or RszFieldType.Float2 or RszFieldType.Point => ConvertArray(variant.AsVector2Array(), static (val) => val.ToRsz()),
                RszFieldType.Vec3 or RszFieldType.Float3 => ConvertArray(variant.AsVector3Array(), static (val) => val.ToRsz()),
                RszFieldType.Vec4 or RszFieldType.Float4 => ConvertArray(variant.AsVector4Array(), static (val) => val.ToRsz()),
                RszFieldType.Position => ConvertArray(variant.AsVector3Array(), static (val) => val.ToRszPosition()),
                RszFieldType.Data => ConvertArray(variant.AsGodotArray<byte[]>().ToArray()),
                RszFieldType.Uint2 => ConvertArray(variant.AsGodotArray<Vector2I>(), static v => v.ToRsz()),
                RszFieldType.Uint3 => ConvertArray(variant.AsGodotArray<Vector3I>(), static v => v.ToRsz()),
                RszFieldType.Uint4 => ConvertArray(variant.AsGodotArray<Vector4I>(), static v => v.ToRsz()),
                RszFieldType.Bool => ConvertArray(variant.AsGodotArray<bool>(), static v => v),
                RszFieldType.Range => ConvertArray(variant.AsGodotArray<Vector2>(), static v => v.ToRszRange()),
                RszFieldType.Size => ConvertArray(variant.AsGodotArray<Vector2>(), static v => v.ToRszSize()),
                RszFieldType.RangeI => ConvertArray(variant.AsGodotArray<Vector2I>(), static v => v.ToRszRange()),
                RszFieldType.OBB => ConvertArray(variant.AsGodotArray<OrientedBoundingBox>(), v => v.ToRsz()),
                RszFieldType.Guid or RszFieldType.Uri => ConvertArray(variant.AsStringArray(), v => Guid.TryParse(v, out var guid) ? guid : Guid.Empty),
                RszFieldType.AABB => ConvertArray(variant.AsGodotArray<Aabb>(), v => v.ToRsz()),
                _ => throw new Exception("Unhandled rsz export array type " + valueType),
            };
        }
        return ToRszStructSingle(variant, valueType, game);
    }
    private static object[] ConvertArray<T>(T[] sourceArray)
    {
        var arr = new object[sourceArray.Length];
        for (int i = 0; i < arr.Length; ++i) {
            arr[i] = (object)(sourceArray[i]!);
        }
        return arr;
    }
    private static object[] ConvertArray<T>(T[] sourceArray, Func<T, object> converter)
    {
        var arr = new object[sourceArray.Length];
        for (int i = 0; i < arr.Length; ++i) {
            arr[i] = converter.Invoke(sourceArray[i]!);
        }
        return arr;
    }
    private static object[] ConvertArray<[MustBeVariant] T>(Godot.Collections.Array<T> sourceArray, Func<T, object> converter)
    {
        var arr = new object[sourceArray.Count];
        for (int i = 0; i < arr.Length; ++i) {
            arr[i] = converter.Invoke(sourceArray[i]!);
        }
        return arr;
    }
    public static object ToRszStructSingle(this Variant variant, RszFieldType valueType, SupportedGame game)
    {
        return valueType switch {
            RszFieldType.S32 => variant.AsInt32(),
            RszFieldType.U32 => variant.AsUInt32(),
            RszFieldType.S64 => variant.AsInt64(),
            RszFieldType.U64 => variant.AsUInt64(),
            RszFieldType.F32 => variant.AsSingle(),
            RszFieldType.F64 => variant.AsDouble(),
            RszFieldType.Bool => variant.AsBool(),
            RszFieldType.S8 => variant.AsSByte(),
            RszFieldType.U8 => variant.AsByte(),
            RszFieldType.S16 => variant.AsInt16(),
            RszFieldType.U16 => variant.AsUInt16(),
            RszFieldType.Data => variant.AsByteArray(),
            RszFieldType.Mat4 => variant.AsProjection().ToRsz(),
            RszFieldType.Vec2 or RszFieldType.Float2 or RszFieldType.Point => variant.AsVector2().ToRsz(),
            RszFieldType.Vec3 or RszFieldType.Float3 => variant.AsVector3().ToRsz(),
            RszFieldType.Position => variant.AsVector3().ToRszPosition(),
            RszFieldType.Vec4 or RszFieldType.Float4 => variant.AsVector4().ToRsz(),
            RszFieldType.Int2 => (ReeLib.via.Int2)variant.AsVector2I().ToRsz(),
            RszFieldType.Int3 => (ReeLib.via.Int3)variant.AsVector3I().ToRsz(),
            RszFieldType.Int4 => (ReeLib.via.Int4)variant.AsVector4I().ToRsz(),
            RszFieldType.Uint2 => (ReeLib.via.Uint2)variant.AsVector2I().ToRszU(),
            RszFieldType.Uint3 => (ReeLib.via.Uint3)variant.AsVector3I().ToRszU(),
            RszFieldType.Uint4 => (ReeLib.via.Uint4)variant.AsVector4I().ToRszU(),
            RszFieldType.OBB => variant.As<OrientedBoundingBox>().ToRsz(),
            RszFieldType.AABB => (ReeLib.via.AABB)variant.AsAabb().ToRsz(),
            RszFieldType.Guid => Guid.TryParse(variant.AsString(), out var guid) ? guid : Guid.Empty,
            RszFieldType.Uri => variant.VariantType == Variant.Type.String
                ? Guid.TryParse(variant.AsString(), out var guid) ? guid : Guid.Empty
                : variant.As<GameObjectRef>().TargetGuid,
            RszFieldType.GameObjectRef => variant.As<GameObjectRef>().TargetGuid,
            RszFieldType.Color => (ReeLib.via.Color)variant.AsColor().ToRsz(),
            RszFieldType.Range => (ReeLib.via.Range)variant.AsVector2().ToRszRange(),
            RszFieldType.RangeI => (ReeLib.via.RangeI)variant.AsVector2I().ToRszRange(),
            RszFieldType.Quaternion => variant.AsQuaternion().ToRsz(),
            RszFieldType.Sphere => variant.AsVector4().ToSphere(),
            RszFieldType.Capsule => variant.As<Capsule>().ToRsz(),
            RszFieldType.Area => variant.As<Area>().ToRsz(),
            RszFieldType.TaperedCapsule => variant.As<TaperedCapsule>().ToRsz(),
            RszFieldType.Cone => variant.As<Cone>().ToRsz(),
            RszFieldType.Line => variant.As<Line>().ToRsz(),
            RszFieldType.LineSegment => variant.As<LineSegment>().ToRsz(),
            RszFieldType.Plane => variant.As<Plane>().ToRsz(),
            RszFieldType.PlaneXZ => new ReeLib.via.PlaneXZ { dist = variant.AsSingle() },
            RszFieldType.Size => new ReeLib.via.Size() { w = variant.AsVector2().X, h = variant.AsVector2().Y },
            RszFieldType.Ray => variant.As<Ray>().ToRsz(),
            RszFieldType.RayY => variant.As<RayY>().ToRsz(),
            RszFieldType.Segment => variant.As<Segment>().ToRsz(),
            RszFieldType.Triangle => variant.As<Triangle>().ToRsz(),
            RszFieldType.Cylinder => variant.As<Cylinder>().ToRsz(),
            RszFieldType.Ellipsoid => variant.As<Ellipsoid>().ToRsz(),
            RszFieldType.Torus => variant.As<Torus>().ToRsz(),
            RszFieldType.Rect => variant.As<Vector4>().ToRect(),
            RszFieldType.Rect3D => variant.As<Rect3D>().ToRsz(),
            RszFieldType.Frustum => variant.As<Frustum>().ToRsz(),
            RszFieldType.KeyFrame => variant.As<KeyFrame>().ToRsz(),
            RszFieldType.Sfix => new ReeLib.via.sfix() { v = variant.AsInt32() },
            RszFieldType.Sfix2 => variant.AsVector2I().ToSfix(),
            RszFieldType.Sfix3 => variant.AsVector3I().ToSfix(),
            RszFieldType.Sfix4 => variant.AsVector4I().ToSfix(),
            RszFieldType.String => variant.AsString(),
            RszFieldType.RuntimeType => variant.AsString(),
            RszFieldType.Enum => variant.AsInt32(),
            _ => throw new Exception("No defined conversion to RSZ type " + valueType),
        };
    }

    public static Vector2 ToGodot(this System.Numerics.Vector2 val) => new Vector2(val.X, val.Y);
    public static Vector3 ToGodot(this System.Numerics.Vector3 val) => new Vector3(val.X, val.Y, val.Z);
    // not ideal but we can't use double positions nicely in godot so, hopefully not to lossy
    public static Vector3 ToGodot(this ReeLib.via.Position val) => new Vector3((float)val.x, (float)val.y, (float)val.z);
    public static Vector2I ToGodot(this ReeLib.via.Int2 val) => new Vector2I(val.x, val.y);
    public static Vector3I ToGodot(this ReeLib.via.Int3 val) => new Vector3I(val.x, val.y, val.z);
    public static Vector4 ToGodot(this System.Numerics.Vector4 val) => new Vector4(val.X, val.Y, val.Z, val.W);
    public static Vector4 ToGodot(this ReeLib.via.Rect val) => new Vector4(val.t, val.r, val.b, val.l);
    public static Vector4 ToVector4(this ReeLib.via.Sphere val) => new Vector4(val.pos.X, val.pos.Y, val.Pos.Z, val.R);
    public static Vector4 ToVector4(this Vector3 val) => new Vector4(val.X, val.Y, val.Z, 0);
    public static Vector4 ToVector4(this Quaternion val) => new Vector4(val.X, val.Y, val.Z, val.W);
    public static Transform3D ToGodot(this ReeLib.via.Transform transform) => new Transform3D(new Basis(transform.rot.ToGodot()).Scaled(transform.scale.ToGodot()), transform.pos.ToGodot());
    public static Quaternion ToGodot(this System.Numerics.Quaternion val) => new Quaternion(val.X, val.Y, val.Z, val.W);
    public static Aabb ToGodot(this ReeLib.via.AABB val) => new Aabb(val.minpos.ToGodot(), val.maxpos.ToGodot() - val.minpos.ToGodot());
    public static Godot.Color ToGodot(this ReeLib.via.Color val) => new Godot.Color(SwapEndianness(val.rgba));  // godot's interpretation of RGBA is 0xff000000 = R and 0xff = A

    public static ReeLib.via.Sfix2 ToSfix(this Vector2I vec) => new() { x = new sfix() { v = vec.X }, y = new sfix() { v = vec.Y } };
    public static ReeLib.via.Sfix3 ToSfix(this Vector3I vec) => new() { x = new sfix() { v = vec.X }, y = new sfix() { v = vec.Y }, z = new sfix() { v = vec.Z } };
    public static ReeLib.via.Sfix4 ToSfix(this Vector4I vec) => new() { x = new sfix() { v = vec.X }, y = new sfix() { v = vec.Y }, z = new sfix() { v = vec.Z }, w = new sfix() { v = vec.W } };
    public static ReeLib.via.Sphere ToSphere(this Vector4 val) => new ReeLib.via.Sphere { pos = val.ToVector3().ToRsz(), r = val.W };
    public static ReeLib.via.Rect ToRect(this Vector4 val) => new ReeLib.via.Rect { t = val.X, r = val.Y, b = val.Z, l = val.W  };
    public static ReeLib.via.AABB ToRsz(this Aabb val) => new() {
        minpos = val.Position.ToRsz(),
        maxpos = val.End.ToRsz(),
    };
    public static ReeLib.via.Transform ToRszTransform(this Transform3D transform) => new Transform() {
        pos = transform.Origin.ToRsz(),
        rot = transform.Basis.GetRotationQuaternion().ToRsz(),
        scale = transform.Basis.Scale.ToRsz(),
    };
    public static Projection ToProjection(this ReeLib.via.mat4 mat)
    {
        return new Projection(
            new Vector4(mat.m00, mat.m01, mat.m02, mat.m03),
            new Vector4(mat.m10, mat.m11, mat.m12, mat.m13),
            new Vector4(mat.m20, mat.m21, mat.m22, mat.m23),
            new Vector4(mat.m30, mat.m31, mat.m32, mat.m33)
        );
    }

    public static System.Numerics.Vector2 ToRsz(this Vector2 val) => new System.Numerics.Vector2(val.X, val.Y);
    public static ReeLib.via.Range ToRszRange(this Vector2 val) => new ReeLib.via.Range { r = val.X, s = val.Y };
    public static ReeLib.via.Size ToRszSize(this Vector2 val) => new ReeLib.via.Size { w = val.X, h = val.Y };
    public static ReeLib.via.RangeI ToRszRange(this Vector2I val) => new ReeLib.via.RangeI { r = val.X, s = val.Y };
    public static System.Numerics.Vector3 ToRsz(this Vector3 val) => new System.Numerics.Vector3(val.X, val.Y, val.Z);
    public static ReeLib.via.Position ToRszPosition(this Vector3 val) => new Position  { x = val.X, y = val.Y, z = val.Z };
    public static System.Numerics.Vector4 ToRsz(this Vector4 val) => new System.Numerics.Vector4(val.X, val.Y, val.Z, val.W);
    public static ReeLib.via.Int2 ToRsz(this Vector2I val) => new ReeLib.via.Int2 { x = val.X, y = val.Y };
    public static ReeLib.via.Int3 ToRsz(this Vector3I val) => new ReeLib.via.Int3 { x = val.X, y = val.Y, z = val.Z };
    public static ReeLib.via.Int4 ToRsz(this Vector4I val) => new ReeLib.via.Int4 { x = val.X, y = val.Y, z = val.Z, w = val.W };
    public static ReeLib.via.Color ToRsz(this Godot.Color val) => new ReeLib.via.Color { rgba = val.ToAbgr32() };
    public static System.Numerics.Quaternion ToRsz(this Quaternion val) => new System.Numerics.Quaternion(val.X, val.Y, val.Z, val.W);
    public static ReeLib.via.Uint2 ToRszU(this Vector2I val) => new ReeLib.via.Uint2 { x = (uint)val.X, y = (uint)val.Y };
    public static ReeLib.via.Uint3 ToRszU(this Vector3I val) => new ReeLib.via.Uint3 { x = (uint)val.X, y = (uint)val.Y, z = (uint)val.Z };
    public static ReeLib.via.Uint4 ToRszU(this Vector4I val) => new ReeLib.via.Uint4 { x = (uint)val.X, y = (uint)val.Y, z = (uint)val.Z, w = (uint)val.W };
    public static ReeLib.via.mat4 ToRsz(this Projection val)
    {
        return new mat4() {
            m00 = val.X.X,
            m01 = val.X.Y,
            m02 = val.X.Z,
            m03 = val.X.W,
            m10 = val.Y.X,
            m11 = val.Y.Y,
            m12 = val.Y.Z,
            m13 = val.Y.W,
            m20 = val.Z.X,
            m21 = val.Z.Y,
            m22 = val.Z.Z,
            m23 = val.Z.W,
            m30 = val.W.X,
            m31 = val.W.Y,
            m32 = val.W.Z,
            m33 = val.W.W,
        };
    }
}