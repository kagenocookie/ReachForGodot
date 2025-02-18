namespace RGE;

using System;
using System.Text.Json;
using Godot;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using REFDumpFormatter;
using RszTool;
using RszTool.via;

public class TypeCache
{
    private static readonly Dictionary<SupportedGame, RszParser> rszData = new();
    private static readonly Dictionary<SupportedGame, Dictionary<string, REObjectTypeCache>> cache = new();
    private static readonly Dictionary<SupportedGame, Dictionary<string, EnumDescriptor>> enums = new();

    static TypeCache()
    {
        System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(typeof(RszGodotConverter).Assembly)!.Unloading += (c) => {
            rszData.Clear();
            cache.Clear();
            enums.Clear();
        };
    }

    public static REObjectTypeCache GetData(SupportedGame game, string classname)
    {
        if (!cache.TryGetValue(game, out var cacheData)) {
            cache[game] = cacheData = new();
        }

        if (!cacheData.TryGetValue(classname, out var data)) {
            var cls = GetRszClass(game, classname);
            if (cls != null) {
                cacheData[classname] = data = GenerateObjectCache(cls, game);
            } else {
                cacheData[classname] = data = REObjectTypeCache.Empty;
            }
        }
        return data;
    }

    private static REObjectTypeCache GenerateObjectCache(RszClass cls, SupportedGame game)
    {
        return new REObjectTypeCache(GenerateFields(cls, game));
    }

    public static RszClass? GetRszClass(SupportedGame game, string classname)
    {
        if (!rszData.TryGetValue(game, out var data)) {
            rszData[game] = data = LoadRsz(game);
        }

        return data.GetRSZClass(classname);
    }

    public static REField[] GenerateFields(RszClass cls, SupportedGame game)
    {
        var fields = new REField[cls.fields.Length];
        for (int i = 0; i < cls.fields.Length; ++i) {
            var srcField = cls.fields[i];
            var refield = new REField() {
                RszField = srcField,
                FieldIndex = cls.IndexOfField(srcField.name)
            };
            fields[i] = refield;
            RszFieldToVariantType(srcField, refield, game);
        }
        return fields;
    }

    private static void RszFieldToVariantType(RszField srcField, REField refield, SupportedGame game)
    {
        if (srcField.array) {
            switch (srcField.type) {
                case RszFieldType.U8:
                    refield.VariantType = Variant.Type.PackedByteArray;
                    return;
                case RszFieldType.S32:
                    refield.VariantType = Variant.Type.PackedInt32Array;
                    return;
                case RszFieldType.S64:
                    refield.VariantType = Variant.Type.PackedInt64Array;
                    return;
                case RszFieldType.F32:
                    refield.VariantType = Variant.Type.PackedFloat32Array;
                    return;
                case RszFieldType.F64:
                    refield.VariantType = Variant.Type.PackedFloat64Array;
                    return;
                case RszFieldType.Color:
                    refield.VariantType = Variant.Type.PackedColorArray;
                    return;
                case RszFieldType.Vec2:
                case RszFieldType.Float2:
                case RszFieldType.Size:
                    refield.VariantType = Variant.Type.PackedVector2Array;
                    return;
                case RszFieldType.Vec3:
                case RszFieldType.Float3:
                case RszFieldType.Position:
                    refield.VariantType = Variant.Type.PackedVector3Array;
                    return;
                case RszFieldType.Vec4:
                case RszFieldType.Float4:
                    refield.VariantType = Variant.Type.PackedVector4Array;
                    return;
                case RszFieldType.String:
                    refield.VariantType = Variant.Type.PackedStringArray;
                    return;
                case RszFieldType.UserData:
                    refield.VariantType = Variant.Type.Array;
                    refield.Hint = PropertyHint.ResourceType;
                    refield.HintString = nameof(REObject);
                    break;
                case RszFieldType.Resource:
                    refield.VariantType = Variant.Type.Array;
                    refield.Hint = PropertyHint.ResourceType;
                    return;
                default:
                    refield.VariantType = Variant.Type.Array;
                    return;
            }
        }
        switch (srcField.type) {
            case RszFieldType.Object:
            case RszFieldType.UserData:
                refield.VariantType = Variant.Type.Object;
                refield.Hint = PropertyHint.ResourceType;
                refield.HintString = nameof(REObject);
                break;
            case RszFieldType.S8:
            case RszFieldType.S16:
            case RszFieldType.S32:
            case RszFieldType.S64:
            case RszFieldType.U8:
            case RszFieldType.U16:
            case RszFieldType.U32:
            case RszFieldType.U64:
                refield.VariantType = Variant.Type.Int;
                // TODO original type? display type?
                if (!string.IsNullOrEmpty(srcField.original_type)) {
                    var desc = GetEnumDescriptor(game, srcField.original_type);
                    if (desc != null && !desc.IsEmpty) {
                        // use Enum and not EnumSuggestion
                        // TODO for custom enum values, provide a way to explicitly define them somewhere in a config
                        // maybe coordinate with content editor's fake entity enums
                        refield.Hint = PropertyHint.Enum;
                        refield.HintString = desc.HintstringLabels;
                    }
                }
                break;
            case RszFieldType.Sfix:
                refield.VariantType = Variant.Type.Int;
                break;
            case RszFieldType.F16:
            case RszFieldType.F32:
            case RszFieldType.F64:
                refield.VariantType = Variant.Type.Float;
                break;
            case RszFieldType.String:
                refield.VariantType = Variant.Type.String;
                break;
            case RszFieldType.Bool:
                refield.VariantType = Variant.Type.Bool;
                break;
            case RszFieldType.Int2:
            case RszFieldType.Uint2:
                refield.VariantType = Variant.Type.Vector2I;
                break;
            case RszFieldType.Int3:
            case RszFieldType.Uint3:
                refield.VariantType = Variant.Type.Vector3I;
                break;
            case RszFieldType.Int4:
            case RszFieldType.Uint4:
                refield.VariantType = Variant.Type.Vector4I;
                break;
            case RszFieldType.Vec2:
            case RszFieldType.Float2:
            case RszFieldType.Point:
            case RszFieldType.Sfix2:
                refield.VariantType = Variant.Type.Vector2;
                break;
            case RszFieldType.Vec3:
            case RszFieldType.Float3:
            case RszFieldType.Position:
            case RszFieldType.Sfix3:
                refield.VariantType = Variant.Type.Vector3;
                break;
            case RszFieldType.Vec4:
            case RszFieldType.Float4:
            case RszFieldType.Sfix4:
                refield.VariantType = Variant.Type.Vector4;
                break;
            case RszFieldType.Data:
                refield.VariantType = Variant.Type.PackedByteArray;
                break;
            case RszFieldType.Mat4:
                refield.VariantType = Variant.Type.Transform3D;
                break;
            case RszFieldType.AABB:
                refield.VariantType = Variant.Type.Aabb;
                break;
            case RszFieldType.Guid:
            case RszFieldType.GameObjectRef:
            case RszFieldType.Uri:
                refield.VariantType = Variant.Type.String;
                break;
            case RszFieldType.Color:
                refield.VariantType = Variant.Type.Color;
                break;
            case RszFieldType.Range:
                refield.VariantType = Variant.Type.Vector2;
                break;
            case RszFieldType.RangeI:
                refield.VariantType = Variant.Type.Vector2I;
                break;
            case RszFieldType.Quaternion:
                refield.VariantType = Variant.Type.Quaternion;
                break;
            case RszFieldType.Size:
                refield.VariantType = Variant.Type.Vector2;
                break;

            // TODO see if we can rename the individual inputs maybe?
            case RszFieldType.Sphere:
                refield.VariantType = Variant.Type.Quaternion;
                break;
            case RszFieldType.Rect:
                refield.VariantType = Variant.Type.Quaternion;
                break;

            // TODO unhandled types
            case RszFieldType.LineSegment:
                // new RszTool.via.LineSegment();
                break;
            case RszFieldType.Plane:
                // new RszTool.via.Plane();
                break;
            case RszFieldType.PlaneXZ:
                // new RszTool.via.PlaneXZ();
                break;
            case RszFieldType.Ray:
                // new RszTool.via.Ray();
                break;
            case RszFieldType.RayY:
                // new RszTool.via.RayY();
                break;
            case RszFieldType.Segment:
                // new RszTool.via.Segment();
                break;
            case RszFieldType.Triangle:
                // new RszTool.via.Triangle();
                break;
            case RszFieldType.Cylinder:
                // new RszTool.via.Cylinder();
                break;
            case RszFieldType.Ellipsoid:
                // new RszTool.via.Ellipsoid();
                break;
            case RszFieldType.Torus:
                // new RszTool.via.Torus();
                break;
            case RszFieldType.Frustum:
                // new RszTool.via.Frustum();
                break;
            case RszFieldType.KeyFrame:
                // new RszTool.via.KeyFrame();
                break;
            case RszFieldType.Rect3D:
                // new RszTool.via.Rect3D();
                break;
            case RszFieldType.OBB:
                // new RszTool.via.OBB();
                break;
            case RszFieldType.Capsule:
                // new RszTool.via.Capsule();
                break;
            case RszFieldType.Area:
                // new RszTool.via.Area();
                break;
            case RszFieldType.TaperedCapsule:
                // new RszTool.via.TaperedCapsule();
                break;
            case RszFieldType.Cone:
                // new RszTool.via.Cone();
                break;
            case RszFieldType.Line:
                // new RszTool.via.Line();
                break;
            case RszFieldType.Resource:
                // var res = Importer.FindOrImportResource<REResource>()
                break;
            default:
                refield.VariantType = Variant.Type.Nil;
                GD.Print("Unhandled rsz field type " + srcField.type + " / " + srcField.original_type);
                break;
        }
    }

    private static RszParser LoadRsz(SupportedGame game)
    {
        var jsonPath = ReachForGodot.GetPaths(game)?.RszJsonPath;
        if (jsonPath == null) {
            GD.PrintErr("No rsz json defined for game " + game);
            return null!;
        }

        return rszData[game] = RszParser.GetInstance(jsonPath);
    }

    public static EnumDescriptor GetEnumDescriptor(SupportedGame game, string classname)
    {
        if (!enums.TryGetValue(game, out var list)) {
            enums[game] = list = new();
            var inputFilepath = ReachForGodot.GetAssetConfig(game).Paths.Il2cppPath;
            if (inputFilepath == null) {
                return EnumDescriptor<int>.Default;
            }

            // TODO add file cache for specific il2cpp data (enums, anything else we might need) since it's slow

            using var fs = File.OpenRead(inputFilepath);
            var entries = System.Text.Json.JsonSerializer.Deserialize<REFDumpFormatter.SourceDumpRoot>(fs)
                ?? throw new Exception("File is not a valid dump json file");
            fs.Close();
            foreach (var e in entries) {
                if (e.Value.parent == "System.Enum") {
                    var backing = REFDumpFormatter.EnumParser.GetEnumBackingType(e.Value);
                    if (backing == null) {
                        GD.PrintErr("Couldn't determine enum backing type: " + e.Key);
                        list[e.Key] = EnumDescriptor<int>.Default;
                        continue;
                    }
                    var enumType = typeof(EnumDescriptor<>).MakeGenericType(backing);
                    var descriptior = (EnumDescriptor)Activator.CreateInstance(enumType)!;
                    descriptior.ParseData(e.Value);
                    list[e.Key] = descriptior;
                }
            }
        }

        if (list.TryGetValue(classname, out var desc)) {
            return desc;
        }

        return EnumDescriptor<int>.Default;
    }
}

public abstract class EnumDescriptor
{
    public abstract string GetLabel(object value);
    // public abstract object GetValue(string label);

    protected abstract IEnumerable<string> LabelValuePairs { get; }
    private string? _hintstring;
    public string HintstringLabels => _hintstring ??= string.Join(",", LabelValuePairs);

    public bool IsEmpty { get; private set; } = true;

    public void ParseData(ObjectDef item)
    {
        if (item.fields == null) return;

        foreach (var (name, field) in item.fields.OrderBy(f => f.Value.Id)) {
            if (!field.Flags.Contains("SpecialName") && field.IsStatic && field.Default is JsonElement elem && elem.ValueKind == JsonValueKind.Number) {
                AddValue(name, elem);
            }
        }

        IsEmpty = false;
    }

    protected abstract void AddValue(string name, JsonElement elem);
}

public sealed class EnumDescriptor<T> : EnumDescriptor where T : struct
{
    public readonly Dictionary<T, string> ValueToLabels = new();
    // public readonly Dictionary<string, T> LabelToValues = new();

    public static readonly EnumDescriptor<T> Default = new();
    private static readonly object DefaultValue = default(T);

    public override string GetLabel(object value) => ValueToLabels.TryGetValue((T)value, out var val) ? val : string.Empty;

    // public override object GetValue(string label) => LabelToValues.TryGetValue(label, out var val) ? val : DefaultValue;
    private static Func<JsonElement, T>? converter;

    protected override IEnumerable<string> LabelValuePairs => ValueToLabels.Select((pair) => $"{pair.Value}:{pair.Key}");

    protected override void AddValue(string name, JsonElement elem)
    {
        if (converter == null) {
            CreateConverter();
        }
        T val = converter!(elem);
        ValueToLabels[val] = name;
    }

    private static void CreateConverter()
    {
        // nasty; maybe add individual enum descriptor types eventually
        if (typeof(T) == typeof(System.Int64)) {
            converter = static (e) => (T)Convert.ChangeType(e.GetUInt64(), typeof(System.Int64));
        } else if (typeof(T) == typeof(System.UInt64)) {
            converter = static (e) => (T)(object)e.GetUInt64();
        } else if (typeof(T) == typeof(System.Int32)) {
            converter = static (e) => {
                var v = e.GetInt64();
                return (T)(object)(int)(v >= 2147483648 ? (v - 2 * 2147483648L) : v);
            };
        } else if (typeof(T) == typeof(System.UInt32)) {
            converter = static (e) => (T)(object)e.GetUInt32();
        } else if (typeof(T) == typeof(System.Int16)) {
            converter = static (e) => {
                var v = e.GetInt32();
                return (T)(object)(short)(v >= 32768 ? (v - 2 * 32768) : v);
            };
        } else if (typeof(T) == typeof(System.UInt16)) {
            converter = static (e) => (T)(object)e.GetUInt16();
        } else if (typeof(T) == typeof(System.SByte)) {
            converter = static (e) => {
                var v = e.GetInt32();
                return (T)(object)(sbyte)(v >= 128 ? (v - 2 * 128) : v);
            };
        } else if (typeof(T) == typeof(System.Byte)) {
            converter = static (e) => (T)(object)e.GetByte();
        } else {
            converter = static (e) => default(T);
        }
    }
}


public class REField
{
    public required RszField RszField { get; init; }
    public required int FieldIndex { get; init; }
    public Variant.Type VariantType { get; set; }
    public string? DisplayName { get; set; }
    public PropertyHint Hint { get; set; }
    public string? HintString { get; set; }

    public string SerializedName => RszField.name;
}

public class REObjectTypeCache
{
    public static readonly REObjectTypeCache Empty = new REObjectTypeCache(Array.Empty<REField>());

    public REField[] Fields { get; }
    public Dictionary<string, REField> FieldsByName { get; }
    public Godot.Collections.Array<Godot.Collections.Dictionary> PropertyList { get; }

    public REObjectTypeCache(REField[] fields)
    {
        Fields = fields;
        PropertyList = new();
        FieldsByName = new(fields.Length);
        PropertyList.Add(new Godot.Collections.Dictionary()
        {
            { "name", "RSZ Data" },
            { "type", (int)Variant.Type.Nil },
            { "usage", (int)(PropertyUsageFlags.Category|PropertyUsageFlags.ScriptVariable) }
        });
        foreach (var f in fields) {
            FieldsByName[f.SerializedName] = f;

            var dict = new Godot.Collections.Dictionary()
            {
                // TODO include "class_name"?
                { "name", f.DisplayName ?? f.SerializedName },
                { "type", (int)f.VariantType },
                { "hint", (int)f.Hint },
                { "usage", (int)(PropertyUsageFlags.Editor|PropertyUsageFlags.ScriptVariable) }
            };
            if (f.HintString != null) dict["hint_string"] = f.HintString;
            PropertyList.Add(dict);
        }
    }
}