namespace ReaGE;

using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Godot;
using RszTool;

public class TypeCache
{
    private static readonly Dictionary<SupportedGame, PerGameCache> allCacheData = new();

    public static readonly JsonSerializerOptions jsonOptions = new() {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly Dictionary<SupportedGame, Dictionary<string, Func<REGameObject, RszInstance, REComponent?>>> perGameFactories = new();

    private sealed class PerGameCache
    {
        public RszParser? parser;
        public Dictionary<string, REObjectTypeCache>? serializationCache;
        public Il2cppCache? il2CppCache;
        public Dictionary<string, Dictionary<string, PrefabGameObjectRefProperty>>? gameObjectRefProps;
        public Dictionary<string, RszClassPatch>? rszTypePatches;
        public Dictionary<string, List<REObjectFieldAccessor>>? fieldOverrides;
    }

    static TypeCache()
    {
        System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(typeof(GodotRszImporter).Assembly)!.Unloading += (c) => {
            var assembly = typeof(System.Text.Json.JsonSerializerOptions).Assembly;
            var updateHandlerType = assembly.GetType("System.Text.Json.JsonSerializerOptionsUpdateHandler");
            var clearCacheMethod = updateHandlerType?.GetMethod("ClearCache", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            clearCacheMethod!.Invoke(null, new object?[] { null });

            allCacheData.Clear();
        };
        InitComponents(typeof(GodotRszImporter).Assembly);
    }

    public static void InitComponents(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes()) {
            if (!type.IsAbstract && type.GetCustomAttribute<REComponentClassAttribute>() is REComponentClassAttribute attr) {
                if (!type.IsAssignableTo(typeof(REComponent))) {
                    GD.PrintErr($"Invalid REComponentClass annotated type {type.FullName}.\nMust be a non-abstract REComponent node.");
                    continue;
                }
                if (type.GetCustomAttribute<ToolAttribute>() == null || type.GetCustomAttribute<GlobalClassAttribute>() == null) {
                    GD.PrintErr($"REComponentClass annotated type {type.FullName} must also be [Tool] and [GlobalClass].");
                    continue;
                }

                DefineComponentFactory(attr.Classname, (obj, instance) => {
                    var node = (REComponent)Activator.CreateInstance(type)!;
                    return node;
                }, attr.SupportedGames);

                TypeCache.HandleFieldOverrideAttributes(type, attr.Classname, attr.SupportedGames);
            } else if (type.GetCustomAttribute<REObjectClassAttribute>() is REObjectClassAttribute classAttr) {
                if (!type.IsAssignableTo(typeof(REObject))) {
                    GD.PrintErr($"Invalid REObjectClass annotated type {type.FullName}.\nMust be a non-abstract REObject object.");
                    continue;
                }

                if (type.GetCustomAttribute<ToolAttribute>() == null || type.GetCustomAttribute<GlobalClassAttribute>() == null) {
                    GD.PrintErr($"REObjectClass annotated type {type.FullName} must also be [Tool] and [GlobalClass].");
                    continue;
                }

                TypeCache.HandleFieldOverrideAttributes(type, classAttr.Classname, classAttr.SupportedGames);
            }
        }
    }

    public static void DefineComponentFactory(string componentType, Func<REGameObject, RszInstance, REComponent?> factory, params SupportedGame[] supportedGames)
    {
        if (supportedGames.Length == 0) {
            supportedGames = ReachForGodot.GameList;
        }

        foreach (var game in supportedGames) {
            if (!perGameFactories.TryGetValue(game, out var factories)) {
                perGameFactories[game] = factories = new();
            }

            factories[componentType] = factory;
        }
    }

    public static void InitializeGame(SupportedGame game)
    {
        var data = GetCacheRoot(game);
        data.parser ??= LoadRsz(game);
    }

    public static RszFileOption CreateRszFileOptions(AssetConfig config)
    {
        InitializeGame(config.Game);
        return new RszFileOption(
            config.Paths.GetRszToolGameEnum(),
            config.Paths.RszJsonPath ?? throw new Exception("Rsz json file not specified for game " + config.Game));
    }

    public static REObjectTypeCache GetData(SupportedGame game, string classname)
    {
        var baseCache = GetCacheRoot(game);

        var cacheData = baseCache.serializationCache ??= new();
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

    public static void UpdateTypecacheEntry(SupportedGame game, string classname, Dictionary<string, PrefabGameObjectRefProperty> propInfoDict)
    {
        var reflist = GetOrLoadClassProps(game);
        reflist[classname] = propInfoDict;
        GetData(game, classname).PfbRefs = propInfoDict;
        UpdateClassProps(game, reflist);
    }

    private static REObjectTypeCache GenerateObjectCache(RszClass cls, SupportedGame game)
    {
        var cache = new REObjectTypeCache(cls, GenerateFields(cls, game), GetClassProps(game, cls.name));
        var root = GetCacheRoot(game);
        if (root.fieldOverrides?.TryGetValue(cls.name, out var yes) == true) {
            foreach (var accessor in yes) {
                if (accessor.overrideFunc != null) {
                    var field = accessor.Get(game, cache);
                    if (field != null) {
                        accessor.overrideFunc.Invoke(field);
                        var prop = cache.PropertyList.First(dict => dict["name"].AsString() == field.SerializedName);
                        cache.UpdateFieldProperty(field, prop);
                    }
                }
            }
        }

        return cache;
    }

    public static RszClass? GetRszClass(SupportedGame game, string classname)
    {
        if (!allCacheData.TryGetValue(game, out var data)) {
            allCacheData[game] = data = new();
        }

        if (data.parser == null) {
            data.parser = LoadRsz(game);
        }

        return data.parser.GetRSZClass(classname);
    }

    private static PerGameCache GetCacheRoot(SupportedGame game)
    {
        if (!allCacheData.TryGetValue(game, out var data)) {
            allCacheData[game] = data = new();
        }
        return data;
    }

    private static Dictionary<string, Dictionary<string, PrefabGameObjectRefProperty>> GetOrLoadClassProps(SupportedGame game)
    {
        var data = GetCacheRoot(game);
        if (data.gameObjectRefProps != null) return data.gameObjectRefProps;

        var fn = ReachForGodot.GetPaths(game)?.PfbGameObjectRefPropsPath;
        if (File.Exists(fn)) {
            using var fs = File.OpenRead(fn);
            data.gameObjectRefProps = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, PrefabGameObjectRefProperty>>>(fs);
        }
        data.gameObjectRefProps ??= new(0);
        return data.gameObjectRefProps;
    }

    private static Dictionary<string, PrefabGameObjectRefProperty>? GetClassProps(SupportedGame game, string classname)
    {
        var reflist = GetOrLoadClassProps(game);

        if (reflist.TryGetValue(classname, out var result)) {
            return result;
        }

        return null;
    }

    private static void UpdateClassProps(SupportedGame game, Dictionary<string, Dictionary<string, PrefabGameObjectRefProperty>> data)
    {
        var fn = ReachForGodot.GetPaths(game)?.PfbGameObjectRefPropsPath ?? throw new Exception("Missing pfb cache filepath for " + game);
        using var fs = File.Create(fn);
        JsonSerializer.Serialize<Dictionary<string, Dictionary<string, PrefabGameObjectRefProperty>>>(fs, data, jsonOptions);
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

    internal static void HandleFieldOverrideAttributes(Type type, string classname, SupportedGame[] supportedGames)
    {
        foreach (var field in type.GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)) {
            if (field.FieldType == typeof(REObjectFieldAccessor)) {
                var accessor = (REObjectFieldAccessor)field.GetValue(null)!;
                if (accessor.overrideFunc == null) continue;

                var curclass = classname;
                if (field.GetCustomAttribute<REObjectFieldTargetAttribute>() is REObjectFieldTargetAttribute overrideAttr) {
                    curclass = overrideAttr.Classname;
                }

                var games = supportedGames.Length == 0 ? ReachForGodot.GameList : supportedGames;

                foreach (var game in games) {
                    var cache = GetCacheRoot(game);
                    cache.fieldOverrides ??= new();
                    if (!cache.fieldOverrides.TryGetValue(curclass, out var pergame)) {
                        cache.fieldOverrides[curclass] = pergame = new();
                    }
                    pergame.Add(accessor);
                }
            }
        }
    }

    private static void RszFieldToVariantType(RszField srcField, REField refield, SupportedGame game)
    {
        void ResourceHint(REField field, string resourceName)
        {
            field.VariantType = Variant.Type.Object;
            field.Hint = PropertyHint.ResourceType;
            field.HintString = resourceName;
        }

        void ArrayHint(REField field, Variant.Type variantType)
        {
            field.VariantType = Variant.Type.Array;
            field.Hint = PropertyHint.TypeString;
            field.HintString = $"{(int)variantType}/0:";
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
                case RszFieldType.S8:
                case RszFieldType.U16:
                case RszFieldType.S16:
                case RszFieldType.U32:
                case RszFieldType.U64:
                    ArrayHint(refield, Variant.Type.Int);
                    break;
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
                case RszFieldType.RuntimeType:
                    refield.VariantType = Variant.Type.PackedStringArray;
                    return;
                case RszFieldType.Object:
                    refield.VariantType = Variant.Type.Array;
                    refield.Hint = PropertyHint.TypeString;
                    refield.HintString = $"{(int)Variant.Type.Object}/{(int)PropertyHint.ResourceType}:{nameof(UserdataResource)}";
                    return;
                case RszFieldType.UserData:
                    refield.VariantType = Variant.Type.Array;
                    refield.Hint = PropertyHint.TypeString;
                    refield.HintString = $"{(int)Variant.Type.Object}/{(int)PropertyHint.ResourceType}:{nameof(REObject)}";
                    return;
                case RszFieldType.Resource:
                    refield.VariantType = Variant.Type.Array;
                    refield.Hint = PropertyHint.TypeString;
                    refield.HintString = $"{(int)Variant.Type.Object}/{(int)PropertyHint.ResourceType}:{nameof(REResource)}";
                    return;
                case RszFieldType.Data:
                    refield.VariantType = Variant.Type.Array;
                    refield.Hint = PropertyHint.TypeString;
                    refield.HintString = $"{(int)Variant.Type.PackedByteArray}/0:";
                    break;
                default:
                    refield.VariantType = Variant.Type.Array;
                    return;
            }
        }

        switch (srcField.type) {
            case RszFieldType.Object:
            case RszFieldType.UserData:
                ResourceHint(refield, nameof(REObject));
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
            case RszFieldType.RuntimeType:
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
            case RszFieldType.GameObjectRef:
                ResourceHint(refield, nameof(GameObjectRef));
                break;
            case RszFieldType.Guid:
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
                ResourceHint(refield, nameof(REResource));
                break;
            default:
                refield.VariantType = Variant.Type.Nil;
                GD.Print("Unhandled rsz field type " + srcField.type + " / " + srcField.original_type);
                break;
        }
    }

    public static void StoreInferredRszTypes(RSZFile rsz, AssetConfig config)
    {
        var handled = new HashSet<RszClass>();
        var cache = GetCacheRoot(config.Game);
        int changes = 0;
        cache.rszTypePatches ??= new();
        foreach (var inst in rsz.InstanceList) {
            if (!handled.Add(inst.RszClass)) continue;

            foreach (var f in inst.RszClass.fields) {
                if (!f.IsTypeInferred) continue;
                if (f.type < RszFieldType.Undefined) {
                    continue;
                }
                if (!cache.rszTypePatches.TryGetValue(inst.RszClass.name, out var props)) {
                    cache.rszTypePatches[inst.RszClass.name] = props = new();
                }

                var entry = props.FieldPatches?.FirstOrDefault(patch => patch.Name == f.name || patch.ReplaceName == f.name);
                if (entry == null) {
                    entry = new RszFieldPatch() { Name = f.name, Type = f.type };
                    props.FieldPatches = props.FieldPatches == null ? [entry] : props.FieldPatches.Append(entry).ToArray();
                    changes++;
                    RebuildSingleFieldCache(cache, inst.RszClass.name, f.name, config.Game);
                } else if (entry.Type != f.type) {
                    entry.Type = f.type;
                    changes++;
                    RebuildSingleFieldCache(cache, inst.RszClass.name, f.name, config.Game);
                }
            }
        }
        if (changes > 0) {
            UpdateRszPatches(config);
            GD.Print($"Updating RSZ inferred field type cache with {changes} changes");
        }
    }

    private static void RebuildSingleFieldCache(PerGameCache cache, string classname, string field, SupportedGame game)
    {
        if (cache.serializationCache?.TryGetValue(classname, out var cls) == true) {
            var fieldObj = cls.GetFieldByName(field);
            var prop = cls.PropertyList.First(dict => dict["name"].AsString() == field);
            Debug.Assert(fieldObj != null);
            Debug.Assert(prop != null);
            RszFieldToVariantType(fieldObj.RszField, fieldObj, game);
            cls.UpdateFieldProperty(fieldObj, prop);
        }
    }

    private static void UpdateRszPatches(AssetConfig config)
    {
        var cache = GetCacheRoot(config.Game);
        Directory.CreateDirectory(config.Paths.RszPatchPath.GetBaseDir());
        using var file = File.Create(config.Paths.RszPatchPath);
        JsonSerializer.Serialize(file, cache.rszTypePatches, jsonOptions);
    }

    private static RszParser LoadRsz(SupportedGame game)
    {
        var paths = ReachForGodot.GetPaths(game);
        var jsonPath = paths?.RszJsonPath;
        if (jsonPath == null || paths == null) {
            GD.PrintErr("No rsz json defined for game " + game);
            return null!;
        }

        var config = GetCacheRoot(game);

        GD.Print("Loading RSZ data...");
        var time = new Stopwatch();
        time.Start();
        var parser = RszParser.GetInstance(jsonPath);
        parser.ReadPatch(GamePaths.RszPatchGlobalPath);
        if (File.Exists(paths.RszPatchPath)) {
            using FileStream fileStream = File.OpenRead(paths.RszPatchPath);
            config.rszTypePatches = JsonSerializer.Deserialize<Dictionary<string, RszClassPatch>>(fileStream);
        }
        parser.ReadPatch(paths.RszPatchPath);
        if (config.fieldOverrides != null) {
            foreach (var (cn, accessors) in config.fieldOverrides) {
                foreach (var acc in accessors) {
                    var cls = parser.GetRSZClass(cn)!;
                    GenerateObjectCache(cls, game);
                }
            }
        }
        time.Stop();
        GD.Print("Loaded RSZ data in " + time.Elapsed);
        return parser;
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
        var dataRoot = GetCacheRoot(paths.Game);
        if (dataRoot.il2CppCache != null) return dataRoot.il2CppCache;

        var cache = dataRoot.il2CppCache;

        GD.Print("Loading il2cpp data...");
        var time = new Stopwatch();
        time.Start();
        dataRoot.il2CppCache = cache = new Il2cppCache();
        var baseCacheFile = paths.EnumCacheFilename;
        var overrideFile = paths.EnumOverridesFilename;
        if (File.Exists(baseCacheFile)) {
            if (!File.Exists(paths.Il2cppPath)) {
                var success = TryApplyIl2cppCache(cache, baseCacheFile);
                TryApplyIl2cppCache(cache, overrideFile);
                if (!success) {
                    GD.PrintErr("Failed to load il2cpp cache data from " + baseCacheFile);
                }
                GD.Print("Loaded previously cached il2cpp data in " + time.Elapsed);
                return cache;
            }

            var cacheLastUpdate = File.GetLastWriteTimeUtc(paths.Il2cppPath!);
            var il2cppLastUpdate = File.GetLastWriteTimeUtc(paths.Il2cppPath!);
            if (il2cppLastUpdate <= cacheLastUpdate) {
                var existingCacheWorks = TryApplyIl2cppCache(cache, baseCacheFile);
                TryApplyIl2cppCache(cache, overrideFile);
                GD.Print("Loaded cached il2cpp data in " + time.Elapsed);
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
        GD.Print("Loaded source il2cpp data in " + time.Elapsed);

        GD.Print("Updating il2cpp cache... " + baseCacheFile);
        Directory.CreateDirectory(baseCacheFile.GetBaseDir());
        using var outfs = File.Create(baseCacheFile);
        JsonSerializer.Serialize(outfs, cache.ToCacheData(), jsonOptions);
        outfs.Close();

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

    public static bool TryCreateComponent(SupportedGame game, string classname, REGameObject gameObject, RszInstance instance, out REComponent? componentInfo)
    {
        if (perGameFactories.TryGetValue(game, out var factories) &&
            factories.TryGetValue(classname, out var factory)) {
            componentInfo = factory.Invoke(gameObject, instance);
            if (componentInfo != null) {
                componentInfo.Game = game;
                componentInfo.Classname = classname;
            }
            return true;
        }

        componentInfo = null;
        return false;
    }
}

public class REField
{
    public required RszField RszField { get; init; }
    public required int FieldIndex { get; init; }
    public Variant.Type VariantType { get; set; }
    public PropertyHint Hint { get; set; }
    public string? HintString { get; set; }
    public RESupportedFileFormats ResourceType {
        set => HintString = PathUtils.GetResourceTypeFromFormat(value).Name;
    }

    public string SerializedName => RszField.name;

    public void MarkAsResource(string resourceTypeName)
    {
        RszField.type = RszFieldType.Resource;
        RszField.IsTypeInferred = true;
        VariantType = Variant.Type.Object;
        Hint = PropertyHint.ResourceType;
        HintString = resourceTypeName;
    }

    public void MarkAsType(RszFieldType rszType, string godotResourceTypeName)
    {
        RszField.type = rszType;
        RszField.IsTypeInferred = true;
        VariantType = Variant.Type.Object;
        Hint = PropertyHint.ResourceType;
        HintString = godotResourceTypeName;
    }
}

public class REObjectFieldCondition
{
    public REObjectFieldConditionFunc func;

    public REObjectFieldCondition(REObjectFieldConditionFunc func)
    {
        this.func = func;
    }

    public static implicit operator REObjectFieldCondition(string name)
        => new REObjectFieldCondition((fs) => fs.FirstOrDefault(f => f.SerializedName == name));
    public static implicit operator REObjectFieldCondition(REObjectFieldConditionFunc condition)
        => new REObjectFieldCondition(condition);
}

public delegate REField? REObjectFieldConditionFunc(REField[] fields);

public class REObjectFieldAccessor
{
    public readonly string preferredName;
    private REObjectFieldCondition[] conditions = Array.Empty<REObjectFieldCondition>();
    private Dictionary<SupportedGame, REField?> _cache = new(1);
    public Action<REField>? overrideFunc;

    public REObjectFieldAccessor(string name, Action<REField>? overrideFunc = null)
    {
        preferredName = name;
        this.overrideFunc = overrideFunc;
    }

    public REObjectFieldAccessor(string name, RszFieldType rszType)
    {
        preferredName = name;
        this.overrideFunc = (f) => {
            f.RszField.type = rszType;
        };
    }

    public REObjectFieldAccessor(string name, Type godotResourceType)
    {
        preferredName = name;
        Debug.Assert(godotResourceType.IsAssignableTo(typeof(REResource)));
        this.overrideFunc = (f) => f.MarkAsResource(godotResourceType.Name);
    }

    public REObjectFieldAccessor WithConditions(params REObjectFieldConditionFunc[] conditions)
    {
        this.conditions = conditions.Select(a => new REObjectFieldCondition(a)).ToArray();
        return this;
    }

    public REObjectFieldAccessor WithConditions(params REObjectFieldCondition[] conditions)
    {
        this.conditions = conditions;
        return this;
    }

    public REField Get(REObject target) => Get(target.Game, target.TypeInfo);

    public bool IsMatch(REObject target, StringName name) => Get(target).SerializedName == name;

    public REField Get(SupportedGame game, REObjectTypeCache typecache)
    {
        if (_cache.TryGetValue(game, out var cachedField)) {
            Debug.Assert(cachedField != null);
            return cachedField;
        }

        foreach (var getter in conditions) {
            cachedField = getter.func.Invoke(typecache.Fields)!;
            if (cachedField != null) {
                return _cache[game] = cachedField;
            }
        }

        throw new Exception("Failed to resolve " + typecache.RszClass.name + " field " + preferredName);
    }
}

public class REObjectTypeCache
{
    public static readonly REObjectTypeCache Empty = new REObjectTypeCache(RszClass.Empty, Array.Empty<REField>(), new(0));

    public REField[] Fields { get; }
    public Dictionary<string, REField> FieldsByName { get; }
    public Godot.Collections.Array<Godot.Collections.Dictionary> PropertyList { get; }
    public RszClass RszClass { get; set; }
    public Dictionary<string, PrefabGameObjectRefProperty> PfbRefs { get; set; }
    public bool IsEmpty => RszClass.crc == 0;

    public REField? GetFieldByName(string name) => FieldsByName.TryGetValue(name, out var v) ? v : null;
    public REField GetFieldOrFallback(string name, Func<REField, bool> fallbackFilter)
    {
        if (FieldsByName.TryGetValue(name, out var field)) {
            return field;
        } else {
            var f = Fields.FirstOrDefault(fallbackFilter);
            return f ?? throw new Exception($"Field {name} could not be found for type {RszClass.name}");
        }
    }

    public string GetFieldNameOrFallback(string name, Func<REField, bool> fallbackFilter)
    {
        return GetFieldOrFallback(name, fallbackFilter).SerializedName;
    }

    public REObjectTypeCache(RszClass cls, REField[] fields, Dictionary<string, PrefabGameObjectRefProperty>? prefabRefs)
    {
        PfbRefs = prefabRefs ?? Empty.PfbRefs;
        RszClass = cls;
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
            var dict = new Godot.Collections.Dictionary();
            PropertyList.Add(dict);
            UpdateFieldProperty(f, dict);
        }
    }

    public void UpdateFieldProperty(REField field, Godot.Collections.Dictionary dict)
    {
        dict["name"] = field.SerializedName;
        dict["type"] = (int)field.VariantType;
        dict["hint"] = (int)field.Hint;
        dict["usage"] = (int)(PropertyUsageFlags.Editor|PropertyUsageFlags.ScriptVariable);
        if (field.HintString != null) dict["hint_string"] = field.HintString;
    }
}

public class PrefabGameObjectRefProperty
{
    public int PropertyId { get; set; }
    public bool? AutoDetected { get; set; }
}
