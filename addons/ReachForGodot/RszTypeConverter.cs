namespace ReaGE;

using System;
using System.Globalization;
using Godot;
using RszTool;
using RszTool.via;

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
            if (value == null) return new Godot.Collections.Array();
            if (field.RszField.type == RszFieldType.Data) return (byte[])value;

            var type = value.GetType();
            object[] arr;
            if (type.IsArray) {
                arr = (object[])value;
            } else if (type.IsGenericType && type.GetGenericTypeDefinition() == baseList) {
                arr = ((IList<object>)value).ToArray();
            } else {
                GD.Print("Unhandled array type " + type.FullName);
                arr = Array.Empty<object>();
            }
            var newArray = new Godot.Collections.Array();
            foreach (var v in arr) {
                newArray.Add(FromRszValueSingleValue(field, v, game));
            }
            return newArray;
        }

        return FromRszValueSingleValue(field, value, game);
    }

    public static Variant FromRszValueSingleValue(REField field, object value, SupportedGame game)
    {
        switch (field.RszField.type) {
            case RszFieldType.Resource:
                if (value == null || value is string str && string.IsNullOrEmpty(str)) {
                    return new Variant();
                }
                break;
            case RszFieldType.UserData:
            case RszFieldType.Object:
                GD.PrintErr("Fields of type " + field.RszField.type + " shouldn't be handled from this method!");
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
                return new Vector2(((RszTool.via.Size)value).w, ((RszTool.via.Size)value).h);
            case RszFieldType.Rect:
                return ((RszTool.via.Rect)value).ToGodot();
            case RszFieldType.Range:
                return ((System.Numerics.Vector2)((RszTool.via.Range)value)).ToGodot();
            case RszFieldType.RangeI:
                return new Vector2I(((RszTool.via.RangeI)value).r, ((RszTool.via.RangeI)value).s);
            case RszFieldType.Vec3:
            case RszFieldType.Float3:
            case RszFieldType.Position:
                return ((System.Numerics.Vector3)value).ToGodot();
            case RszFieldType.Vec4:
            case RszFieldType.Float4:
                return ((System.Numerics.Vector4)value).ToGodot();
            case RszFieldType.Quaternion:
                return ((System.Numerics.Quaternion)value).ToGodot();
            case RszFieldType.Color:
                return new Godot.Color(((RszTool.via.Color)value).rgba);
            case RszFieldType.Guid:
            case RszFieldType.Uri:
                return ((Guid)value).ToString();
            case RszFieldType.GameObjectRef:
                return new GameObjectRef((Guid)value);
            case RszFieldType.AABB:
                return ((RszTool.via.AABB)value).ToGodot();
            case RszFieldType.Mat4:
                return ((RszTool.via.mat4)value).ToProjection(game);
            case RszFieldType.OBB:
                return OrientedBoundingBox.FromRsz(((RszTool.via.OBB)value), game);
            case RszFieldType.Sphere:
                return ((RszTool.via.Sphere)value).ToVector4();
            case RszFieldType.LineSegment:
                return (LineSegment)((RszTool.via.LineSegment)value);
            case RszFieldType.Plane:
                return (Plane)((RszTool.via.Plane)value);
            case RszFieldType.PlaneXZ:
                return ((RszTool.via.PlaneXZ)value).dist;
            case RszFieldType.Ray:
                return (Ray)((RszTool.via.Ray)value);
            case RszFieldType.RayY:
                return (RayY)((RszTool.via.RayY)value);
            case RszFieldType.Triangle:
                return (Triangle)((RszTool.via.Triangle)value);
            case RszFieldType.Cylinder:
                return (Cylinder)((RszTool.via.Cylinder)value);
            case RszFieldType.Ellipsoid:
                return (Ellipsoid)((RszTool.via.Ellipsoid)value);
            case RszFieldType.Frustum:
                return (Frustum)((RszTool.via.Frustum)value);
            case RszFieldType.KeyFrame:
                return (KeyFrame)((RszTool.via.KeyFrame)value);
            case RszFieldType.Rect3D:
                return (Rect3D)((RszTool.via.Rect3D)value);
            case RszFieldType.Capsule:
                return (Capsule)((RszTool.via.Capsule)value);
            case RszFieldType.Area:
                return (Area)((RszTool.via.Area)value);
            case RszFieldType.TaperedCapsule:
                return (TaperedCapsule)((RszTool.via.TaperedCapsule)value);
            case RszFieldType.Cone:
                return (Cone)((RszTool.via.Cone)value);
            case RszFieldType.Line:
                return (Line)((RszTool.via.Line)value);
        }

        GD.PrintErr("Unhandled conversion for rsz type " + field.RszField.type + " with value type " + value.GetType().FullName);
        return new Variant();
    }

    public static object? ToRszStruct(this Variant variant, REField field, SupportedGame game)
    {
        if (field.RszField.array) {
            return field.RszField.type switch {
                RszFieldType.S16 => ConvertArray(variant.AsGodotArray<uint>(), static v => v),
                RszFieldType.U16 => ConvertArray(variant.AsGodotArray<uint>(), static v => v),
                RszFieldType.S32 => ConvertArray(variant.AsInt32Array()),
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
                RszFieldType.Vec3 or RszFieldType.Float3 or RszFieldType.Position => ConvertArray(variant.AsVector3Array(), static (val) => val.ToRsz()),
                RszFieldType.Vec4 or RszFieldType.Float4 => ConvertArray(variant.AsVector4Array(), static (val) => val.ToRsz()),
                RszFieldType.Data => ConvertArray(variant.AsGodotArray<byte[]>().ToArray()),
                RszFieldType.Uint2 => ConvertArray(variant.AsGodotArray<Vector2I>(), static v => v.ToRsz()),
                RszFieldType.Uint3 => ConvertArray(variant.AsGodotArray<Vector3I>(), static v => v.ToRsz()),
                RszFieldType.Uint4 => ConvertArray(variant.AsGodotArray<Vector4I>(), static v => v.ToRsz()),
                RszFieldType.Bool => ConvertArray(variant.AsGodotArray<bool>(), static v => v),
                RszFieldType.OBB => ConvertArray(variant.AsGodotArray<OrientedBoundingBox>(), v => v.ToRsz(game)),
                _ => throw new Exception("Unhandled rsz export array type " + field.RszField.type),
            };
        }
        return ToRszStructSingle(variant, field, game);
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
    public static object ToRszStructSingle(this Variant variant, REField field, SupportedGame game)
    {
        return field.RszField.type switch {
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
            RszFieldType.Vec3 or RszFieldType.Float3 or RszFieldType.Position => variant.AsVector3().ToRsz(),
            RszFieldType.Vec4 or RszFieldType.Float4 => variant.AsVector4().ToRsz(),
            RszFieldType.Int2 => (RszTool.via.Int2)variant.AsVector2I().ToRsz(),
            RszFieldType.Int3 => (RszTool.via.Int3)variant.AsVector3I().ToRsz(),
            RszFieldType.Int4 => (RszTool.via.Int4)variant.AsVector4I().ToRsz(),
            RszFieldType.Uint2 => (RszTool.via.Uint2)variant.AsVector2I().ToRszU(),
            RszFieldType.Uint3 => (RszTool.via.Uint3)variant.AsVector3I().ToRszU(),
            RszFieldType.Uint4 => (RszTool.via.Uint4)variant.AsVector4I().ToRszU(),
            RszFieldType.OBB => variant.As<OrientedBoundingBox>().ToRsz(game),
            RszFieldType.AABB => (RszTool.via.AABB)variant.AsAabb().ToRsz(),
            RszFieldType.Guid or RszFieldType.Uri => Guid.TryParse(variant.AsString(), out var guid) ? guid : Guid.Empty,
            RszFieldType.GameObjectRef => variant.As<GameObjectRef>().TargetGuid,
            RszFieldType.Color => (RszTool.via.Color)variant.AsColor().ToRsz(),
            RszFieldType.Range => (RszTool.via.Range)variant.AsVector2().ToRszRange(),
            RszFieldType.RangeI => (RszTool.via.RangeI)variant.AsVector2I().ToRszRange(),
            RszFieldType.Quaternion => variant.AsQuaternion().ToRsz(),
            RszFieldType.Sphere => variant.AsVector4().ToSphere(),
            RszFieldType.Capsule => variant.As<Capsule>().ToRsz(),
            RszFieldType.Area => variant.As<Area>().ToRsz(),
            RszFieldType.TaperedCapsule => variant.As<TaperedCapsule>().ToRsz(),
            RszFieldType.Cone => variant.As<Cone>().ToRsz(),
            RszFieldType.Line => variant.As<Line>().ToRsz(),
            RszFieldType.LineSegment => variant.As<LineSegment>().ToRsz(),
            RszFieldType.Plane => variant.As<Plane>().ToRsz(),
            RszFieldType.PlaneXZ => new RszTool.via.PlaneXZ { dist = variant.AsSingle() },
            RszFieldType.Size => new RszTool.via.Size() { w = variant.AsVector2().X, h = variant.AsVector2().Y },
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
            RszFieldType.Sfix => new RszTool.via.sfix() { v = variant.AsInt32() },
            RszFieldType.Sfix2 => new RszTool.via.Sfix2() { x = new() { v = variant.AsVector2I().X }, y = new() { v = variant.AsVector2I().Y } },
            RszFieldType.Sfix3 => new RszTool.via.Sfix3() { x = new() { v = variant.AsVector3I().X }, y = new() { v = variant.AsVector3I().Y }, z = new() { v = variant.AsVector3I().Z } },
            RszFieldType.Sfix4 => new RszTool.via.Sfix4() { x = new() { v = variant.AsVector4I().X }, y = new() { v = variant.AsVector4I().Y }, z = new() { v = variant.AsVector4I().Z }, w = new() { v = variant.AsVector4I().W } },
            RszFieldType.String => variant.AsString(),
            RszFieldType.RuntimeType => variant.AsString(),
            _ => throw new Exception("No defined conversion to RSZ type " + field.RszField.type),
        };
    }

    public static Vector2 ToGodot(this System.Numerics.Vector2 val) => new Vector2(val.X, val.Y);
    public static Vector3 ToGodot(this System.Numerics.Vector3 val) => new Vector3(val.X, val.Y, val.Z);
    public static Vector4 ToGodot(this System.Numerics.Vector4 val) => new Vector4(val.X, val.Y, val.Z, val.W);
    public static Vector4 ToGodot(this RszTool.via.Rect val) => new Vector4(val.t, val.r, val.b, val.l);
    public static Vector4 ToVector4(this RszTool.via.Sphere val) => new Vector4(val.pos.X, val.pos.Y, val.Pos.Z, val.R);
    public static Vector4 ToVector4(this Vector3 val) => new Vector4(val.X, val.Y, val.Z, 0);
    public static Vector4 ToVector4(this Quaternion val) => new Vector4(val.X, val.Y, val.Z, val.W);
    public static RszTool.via.Sphere ToSphere(this Vector4 val) => new RszTool.via.Sphere { pos = val.ToVector3().ToRsz(), r = val.W };
    public static RszTool.via.Rect ToRect(this Vector4 val) => new RszTool.via.Rect { t = val.X, r = val.Y, b = val.Z, l = val.W  };
    public static Quaternion ToGodot(this System.Numerics.Quaternion val) => new Quaternion(val.X, val.Y, val.Z, val.W);
    public static Aabb ToGodot(this RszTool.via.AABB val) => new Aabb(val.minpos.ToGodot(), val.maxpos.ToGodot() - val.minpos.ToGodot());
    public static RszTool.via.AABB ToRsz(this Aabb val) => new() {
        minpos = val.Position.ToRsz(),
        maxpos = (val.End - val.Position).ToRsz(),
    };
    public static Projection ToProjection(this RszTool.via.mat4 mat, SupportedGame game)
    {
        // the order seems to be game dependent...
        // RE2RT gasstation gimmicks - 0,1,2,3
        // DD2 Dng_05/Env_3517 - 1,2,3,0
        if (game == SupportedGame.ResidentEvil2RT) {
            return new Projection(
                new Vector4(mat.m00, mat.m01, mat.m02, mat.m03),
                new Vector4(mat.m10, mat.m11, mat.m12, mat.m13),
                new Vector4(mat.m20, mat.m21, mat.m22, mat.m23),
                new Vector4(mat.m30, mat.m31, mat.m32, mat.m33)
            );
        }
        return new Projection(
            new Vector4(mat.m10, mat.m11, mat.m12, mat.m13),
            new Vector4(mat.m20, mat.m21, mat.m22, mat.m23),
            new Vector4(mat.m30, mat.m31, mat.m32, mat.m33),
            new Vector4(mat.m00, mat.m01, mat.m02, mat.m03)
        );
    }
    public static System.Numerics.Vector2 ToRsz(this Vector2 val) => new System.Numerics.Vector2(val.X, val.Y);
    public static RszTool.via.Range ToRszRange(this Vector2 val) => new RszTool.via.Range { r = val.X, s = val.Y };
    public static RszTool.via.RangeI ToRszRange(this Vector2I val) => new RszTool.via.RangeI { r = val.X, s = val.Y };
    public static System.Numerics.Vector3 ToRsz(this Vector3 val) => new System.Numerics.Vector3(val.X, val.Y, val.Z);
    public static System.Numerics.Vector4 ToRsz(this Vector4 val) => new System.Numerics.Vector4(val.X, val.Y, val.Z, val.W);
    public static RszTool.via.Int2 ToRsz(this Vector2I val) => new RszTool.via.Int2 { x = val.X, y = val.Y };
    public static RszTool.via.Int3 ToRsz(this Vector3I val) => new RszTool.via.Int3 { x = val.X, y = val.Y, z = val.Z };
    public static RszTool.via.Int4 ToRsz(this Vector4I val) => new RszTool.via.Int4 { x = val.X, y = val.Y, z = val.Z, w = val.W };
    public static RszTool.via.Color ToRsz(this Godot.Color val) => new RszTool.via.Color { rgba = val.ToRgba32() };
    public static System.Numerics.Quaternion ToRsz(this Quaternion val) => new System.Numerics.Quaternion(val.X, val.Y, val.Z, val.W);
    public static RszTool.via.Uint2 ToRszU(this Vector2I val) => new RszTool.via.Uint2 { x = (uint)val.X, y = (uint)val.Y };
    public static RszTool.via.Uint3 ToRszU(this Vector3I val) => new RszTool.via.Uint3 { x = (uint)val.X, y = (uint)val.Y, z = (uint)val.Z };
    public static RszTool.via.Uint4 ToRszU(this Vector4I val) => new RszTool.via.Uint4 { x = (uint)val.X, y = (uint)val.Y, z = (uint)val.Z, w = (uint)val.W };
    public static RszTool.via.mat4 ToRsz(this Projection val)
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