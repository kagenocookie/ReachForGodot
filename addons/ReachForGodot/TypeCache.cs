namespace RGE;

using System;
using System.Text.Json;
using Godot;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RszTool;
using RszTool.via;

public class TypeCache
{
    private static readonly Dictionary<SupportedGame, RszParser> rszData = new();
    private static readonly Dictionary<SupportedGame, Dictionary<string, REObjectTypeCache>> serializationCache = new();
    private static readonly Dictionary<SupportedGame, Il2cppCache> il2cppCache = new();

    private static readonly JsonSerializerOptions jsonOptions = new() {
        WriteIndented = true,
    };

    static TypeCache()
    {
        System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(typeof(RszGodotConverter).Assembly)!.Unloading += (c) => {
            var assembly = typeof(System.Text.Json.JsonSerializerOptions).Assembly;
            var updateHandlerType = assembly.GetType("System.Text.Json.JsonSerializerOptionsUpdateHandler");
            var clearCacheMethod = updateHandlerType?.GetMethod("ClearCache", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            clearCacheMethod!.Invoke(null, new object?[] { null });

            rszData.Clear();
            serializationCache.Clear();
            il2cppCache.Clear();
        };
    }

    public static REObjectTypeCache GetData(SupportedGame game, string classname)
    {
        if (!serializationCache.TryGetValue(game, out var cacheData)) {
            serializationCache[game] = cacheData = new();
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

    private static RszClass? GetRszClass(SupportedGame game, string classname)
    {
        if (!rszData.TryGetValue(game, out var data)) {
            rszData[game] = data = LoadRsz(game);
        }

        return data.GetRSZClass(classname);
    }

    private static REField[] GenerateFields(RszClass cls, SupportedGame game)
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
        void ResourceHint(REField field, string resourceName) {
            field.VariantType = Variant.Type.Object;
            field.Hint = PropertyHint.ResourceType;
            field.HintString = nameof(OrientedBoundingBox);
        }

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
                        // use Enum and not EnumSuggestion so we could still add custom values
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
            case RszFieldType.Mat3:
                refield.VariantType = Variant.Type.Transform3D;
                break;
            case RszFieldType.Mat4:
                refield.VariantType = Variant.Type.Projection;
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

            case RszFieldType.Sphere:
                refield.VariantType = Variant.Type.Vector4;
                break;
            case RszFieldType.Rect:
                refield.VariantType = Variant.Type.Vector4;
                break;
            case RszFieldType.LineSegment:
                ResourceHint(refield, nameof(LineSegment));
                break;
            case RszFieldType.Plane:
                ResourceHint(refield, nameof(Plane));
                break;
            case RszFieldType.PlaneXZ:
                refield.VariantType = Variant.Type.Float;
                break;
            case RszFieldType.Ray:
                ResourceHint(refield, nameof(Ray));
                break;
            case RszFieldType.RayY:
                ResourceHint(refield, nameof(RayY));
                break;
            case RszFieldType.Segment:
                ResourceHint(refield, nameof(Segment));
                break;
            case RszFieldType.Triangle:
                ResourceHint(refield, nameof(Triangle));
                break;
            case RszFieldType.Cylinder:
                ResourceHint(refield, nameof(Cylinder));
                break;
            case RszFieldType.Ellipsoid:
                ResourceHint(refield, nameof(Ellipsoid));
                break;
            case RszFieldType.Torus:
                ResourceHint(refield, nameof(Torus));
                break;
            case RszFieldType.Frustum:
                ResourceHint(refield, nameof(Frustum));
                break;
            case RszFieldType.KeyFrame:
                ResourceHint(refield, nameof(KeyFrame));
                break;
            case RszFieldType.Rect3D:
                ResourceHint(refield, nameof(Rect3D));
                break;
            case RszFieldType.OBB:
                ResourceHint(refield, nameof(OrientedBoundingBox));
                break;
            case RszFieldType.Capsule:
                ResourceHint(refield, nameof(Capsule));
                break;
            case RszFieldType.Area:
                ResourceHint(refield, nameof(Area));
                break;
            case RszFieldType.TaperedCapsule:
                ResourceHint(refield, nameof(TaperedCapsule));
                break;
            case RszFieldType.Cone:
                ResourceHint(refield, nameof(Cone));
                break;
            case RszFieldType.Line:
                ResourceHint(refield, nameof(Line));
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
        var paths = ReachForGodot.GetPaths(game);
        var jsonPath = paths?.RszJsonPath;
        if (jsonPath == null || paths == null) {
            GD.PrintErr("No rsz json defined for game " + game);
            return null!;
        }

        var parser = RszParser.GetInstance(jsonPath);
        parser.ReadPatch(GamePaths.RszPatchGlobalPath);
        parser.ReadPatch(paths.RszPatchPath);
        return rszData[game] = parser;
    }

    public static EnumDescriptor GetEnumDescriptor(SupportedGame game, string classname)
    {
        var cache = SetupIl2cppData(ReachForGodot.GetAssetConfig(game).Paths);
        if (cache.enums.TryGetValue(classname, out var descriptor)) {
            return descriptor;
        }

        return EnumDescriptor<int>.Default;
    }

    private static Il2cppCache SetupIl2cppData(GamePaths paths)
    {
        if (il2cppCache.TryGetValue(paths.Game, out var cache)) {
            return cache;
        }

        GD.Print("Loading il2cpp data...");
        il2cppCache[paths.Game] = cache = new Il2cppCache();
        var baseCacheFile = paths.EnumCacheFilename;
        var overrideFile = paths.EnumOverridesFilename;
        if (File.Exists(baseCacheFile)) {
            if (!File.Exists(paths.Il2cppPath)) {
                var success = TryApplyIl2cppCache(cache, baseCacheFile);
                TryApplyIl2cppCache(cache, overrideFile);
                if (!success) {
                    GD.PrintErr("Failed to load il2cpp cache data from " + baseCacheFile);
                }
                return cache;
            }

            var cacheLastUpdate = File.GetLastWriteTimeUtc(paths.Il2cppPath!);
            var il2cppLastUpdate = File.GetLastWriteTimeUtc(paths.Il2cppPath!);
            if (il2cppLastUpdate <= cacheLastUpdate) {
                var existingCacheWorks = TryApplyIl2cppCache(cache, baseCacheFile);
                TryApplyIl2cppCache(cache, overrideFile);
                if (existingCacheWorks) return cache;
            }
        }

        if (!File.Exists(paths.Il2cppPath)) {
            GD.PrintErr($"Il2cpp file does not exist, nor do we have an enum cache file yet for {paths.Game}. Enums won't show up properly.");
            return cache;
        }

        using var fs = File.OpenRead(paths.Il2cppPath!);
        var entries = System.Text.Json.JsonSerializer.Deserialize<REFDumpFormatter.SourceDumpRoot>(fs)
            ?? throw new Exception("File is not a valid dump json file");
        fs.Close();
        cache.ApplyIl2cppData(entries);

        GD.Print("Updating il2cpp cache... " + baseCacheFile);
        var newCacheJson = JsonSerializer.Serialize(cache.ToCacheData(), jsonOptions);
        Directory.CreateDirectory(baseCacheFile.GetBaseDir());
        File.WriteAllText(baseCacheFile, newCacheJson);

        TryApplyIl2cppCache(cache, overrideFile);
        return cache;
    }

    private static bool TryApplyIl2cppCache(Il2cppCache cache, string filename)
    {
        if (File.Exists(filename)) {
            using var fs = File.OpenRead(filename);
            var data = JsonSerializer.Deserialize<Il2cppCacheData>(fs, jsonOptions);
            if (data != null) {
                cache.ApplyCacheData(data);
                return true;
            } else {
                GD.PrintErr("Invalid il2cpp cache data json file: " + filename);
            }
        }
        return false;
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
