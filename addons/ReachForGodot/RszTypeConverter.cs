namespace RGE;

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

    public static Variant FromRszValueUnsafe(REField field, object value, SupportedGame game)
    {
        if (field.RszField.array) {
            if (value == null) return new Godot.Collections.Array();

            var type = value.GetType();
            object[] arr;
            if (type.IsArray) {
                arr = (object[])value;
            } else if (type.IsGenericType && type.GetGenericTypeDefinition() == baseList) {
                arr = (object[])type.GetMethod("ToArray", System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public)!.Invoke(value, Array.Empty<object?>())!;
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

    private static Variant FromRszValueSingleValue(REField field, object value, SupportedGame game)
    {
        switch (field.RszField.type) {
            case RszFieldType.UserData:
                if (value is RszInstance rsz) {
                    if (rsz.RSZUserData is RSZUserDataInfo ud1) {
                        if (!string.IsNullOrEmpty(ud1.Path)) {
                            return Importer.FindOrImportResource<UserdataResource>(ud1.Path, ReachForGodot.GetAssetConfig(game))!;
                        }
                    } else if (rsz.RSZUserData is RSZUserDataInfo_TDB_LE_67 ud2) {
                        GD.PrintErr("Unsupported userdata reference TDB_LE_67");
                    }
                    if (string.IsNullOrEmpty(rsz.RszClass.name)) {
                        return new Variant();
                    }
                    return new REObject(game, rsz.RszClass.name);
                }

                if (value is not string path || string.IsNullOrWhiteSpace(path)) {
                    return default;
                }

                GD.Print("Fetching userdata file " + path);
                return Importer.FindOrImportResource<UserdataResource>(path, ReachForGodot.GetAssetConfig(game))!;
            case RszFieldType.Object:
                if (value is RszInstance rszInstance) {
                    return new REObject(game, rszInstance.RszClass.name, rszInstance);
                }
                GD.Print("Unhandled rsz object type " + value?.GetType().FullName);
                return default;
            case RszFieldType.Resource:
                if (value is string str && !string.IsNullOrWhiteSpace(str)) {
                    return Importer.FindOrImportResource<Resource>(str, ReachForGodot.GetAssetConfig(game))!;
                } else {
                    return new Variant();
                }

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
            case RszFieldType.GameObjectRef:
                return ((Guid)value).ToString();
            case RszFieldType.AABB:
                return ((RszTool.via.AABB)value).ToGodot();
            case RszFieldType.Mat4:
                return ((RszTool.via.mat4)value).ToProjection();
            case RszFieldType.OBB:
                return (OrientedBoundingBox)(((RszTool.via.OBB)value));
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

    public static Vector2 ToGodot(this System.Numerics.Vector2 val) => new Vector2(val.X, val.Y);
    public static Vector3 ToGodot(this System.Numerics.Vector3 val) => new Vector3(val.X, val.Y, val.Z);
    public static Vector4 ToGodot(this System.Numerics.Vector4 val) => new Vector4(val.X, val.Y, val.Z, val.W);
    public static Vector4 ToVector4(this RszTool.via.Sphere val) => new Vector4(val.pos.X, val.pos.Y, val.Pos.Z, val.R);
    public static Quaternion ToGodot(this System.Numerics.Quaternion val) => new Quaternion(val.X, val.Y, val.Z, val.W);
    public static Aabb ToGodot(this RszTool.via.AABB val) => new Aabb(val.minpos.ToGodot(), val.maxpos.ToGodot() - val.minpos.ToGodot());
    public static Projection ToProjection(this RszTool.via.mat4 mat)
    {
        return new Projection(
            new Vector4(mat.m00, mat.m01, mat.m02, mat.m03),
            new Vector4(mat.m10, mat.m11, mat.m12, mat.m13),
            new Vector4(mat.m20, mat.m21, mat.m22, mat.m23),
            new Vector4(mat.m30, mat.m31, mat.m32, mat.m33)
        );
    }
}