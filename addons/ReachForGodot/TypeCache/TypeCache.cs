namespace ReaGE;

using System;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using ReeLib;
using ReeLib.Il2cpp;

public static partial class TypeCache
{
    static TypeCache()
    {
        System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(typeof(TypeCache).Assembly)!.Unloading += (c) => {
            var assembly = typeof(System.Text.Json.JsonSerializerOptions).Assembly;
            var updateHandlerType = assembly.GetType("System.Text.Json.JsonSerializerOptionsUpdateHandler");
            var clearCacheMethod = updateHandlerType?.GetMethod("ClearCache", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            clearCacheMethod!.Invoke(null, new object?[] { null });
        };
        ResourceRepository.LocalResourceRepositoryFilepath = ProjectSettings.GlobalizePath("res://userdata/resources/cache.json");
        if (ReachForGodot.ReeLibResourceSource != null) {
            ResourceRepository.MetadataRemoteSource = ReachForGodot.ReeLibResourceSource;
        }
        InitResourceFormats(typeof(TypeCache).Assembly);
        InitComponents(typeof(TypeCache).Assembly);
        jsonOptions.Converters.Add(new JsonStringEnumConverter<KnownFileFormats>(allowIntegerValues: false));
        jsonOptions.Converters.Add(new JsonStringEnumConverter<RszFieldType>(allowIntegerValues: false));
        jsonOptions.Converters.Add(new JsonStringEnumConverter<EfxFieldFlags>(allowIntegerValues: false));
    }

    public static readonly JsonSerializerOptions jsonOptions = new() {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault,
    };

    private static readonly Dictionary<SupportedGame, Dictionary<string, Func<GameObject, RszInstance, REComponent?>>> componentFactories = new();

    private static readonly Dictionary<Type, string> componentTypeToClassname = new();

    public static string? GetClassnameForComponentType(Type componentType)
    {
        return componentTypeToClassname.GetValueOrDefault(componentType);
    }

    public static void InitResourceFormats(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes()) {
            if (!type.IsAbstract && type.GetCustomAttribute<ResourceHolderAttribute>() is ResourceHolderAttribute attr) {
                PathUtils.RegisterFileFormat(attr.Format, attr.Extension, type);

                if (type.IsAssignableTo(typeof(REResource))) {
                    Importer.RegisterResource(attr.Format, type);
                }
            }
        }
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

                componentTypeToClassname[type] = attr.Classname;
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

                componentTypeToClassname[type] = classAttr.Classname;
                TypeCache.HandleFieldOverrideAttributes(type, classAttr.Classname, classAttr.SupportedGames);
            } else if (type.GetCustomAttribute<FieldAccessorProviderAttribute>() is FieldAccessorProviderAttribute classAttr2) {
                TypeCache.HandleFieldOverrideAttributes(type, classAttr2.Classname, classAttr2.SupportedGames);
            }
        }
    }

    public static void DefineComponentFactory(string componentType, Func<GameObject, RszInstance, REComponent?> factory, params SupportedGame[] supportedGames)
    {
        if (supportedGames.Length == 0) {
            supportedGames = ReachForGodot.GameList;
        }

        foreach (var game in supportedGames) {
            if (!componentFactories.TryGetValue(game, out var factories)) {
                componentFactories[game] = factories = new();
            }

            factories[componentType] = factory;
        }
    }

    public static bool TryCreateComponent(SupportedGame game, string classname, GameObject gameObject, RszInstance instance, out REComponent? componentInfo)
    {
        if (componentFactories.TryGetValue(game, out var factories) &&
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

    public static ClassInfo GetClassInfo(SupportedGame game, string classname)
    {
        var cache = GetWorkspace(game);

        var cacheKey = (game, classname);
        if (!serializationCache.TryGetValue(cacheKey, out var data)) {
            var cls = cache.RszParser.GetRSZClass(classname);
            if (cls != null) {
                serializationCache[cacheKey] = data = GenerateObjectCache(cache, cls);
            } else {
                serializationCache[cacheKey] = data = ClassInfo.Empty;
            }
        }
        return data;
    }

    public static string GetResourceType(string resourceHolderClassname)
    {
        var format = ReeLib.Il2cpp.TypeCache.GetResourceFormat(resourceHolderClassname);
        if (format != KnownFileFormats.Unknown) {
            return PathUtils.GetResourceTypeFromFormat(format)?.Name ?? nameof(REResource);
        }
        return nameof(REResource);
    }

    private static Dictionary<(SupportedGame, string), ClassInfo> serializationCache = new();
    private static Dictionary<(SupportedGame, string), List<REFieldAccessor>> fieldOverrides = new();

    public static bool ClassExists(SupportedGame game, string classname)
    {
        return GetWorkspace(game).RszParser.GetRSZClass(classname) != null;
    }

    public static List<string> GetSubclasses(SupportedGame game, string baseclass)
    {
        return GetWorkspace(game).TypeCache.GetSubclasses(baseclass);
    }

    private static Workspace GetWorkspace(SupportedGame game)
    {
        return ReachForGodot.GetAssetConfig(game).Workspace;
    }

    private static ClassInfo GenerateObjectCache(Workspace root, RszClass cls)
    {
        var cache = new ClassInfo(cls, GenerateFields(cls, root), root.PfbRefProps.GetValueOrDefault(cls.name));
        var game = root.Config.Game.GameEnum.FromReeLibEnum();
        if (fieldOverrides.TryGetValue((game, cls.name), out var list) == true) {
            foreach (var accessor in list) {
                var field = accessor.Get(game, cache);
                if (field != null) {
                    accessor.Invoke(field);
                    var prop = cache.PropertyList.First(dict => dict["name"].AsString() == field.SerializedName);
                    field.RszField.name = accessor.preferredName;
                    cache.UpdateFieldProperty(field, prop);
                }
            }
        }

        return cache;
    }

    private static REField[] GenerateFields(RszClass cls, Workspace cache)
    {
        var fields = new REField[cls.fields.Length];
        for (int i = 0; i < cls.fields.Length; ++i) {
            var srcField = cls.fields[i];
            var refield = new REField() {
                RszField = srcField,
                FieldIndex = cls.IndexOfField(srcField.name)
            };
            fields[i] = refield;
            RszFieldToGodotProperty(srcField, refield, cache);
        }
        return fields;
    }

    private static void HandleFieldOverrideAttributes(Type type, string? classname, SupportedGame[] supportedGames)
    {
        foreach (var field in type.GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)) {
            if (field.FieldType == typeof(REFieldAccessor)) {
                var accessor = (REFieldAccessor)field.GetValue(null)!;

                var curclass = classname;
                if (field.GetCustomAttribute<REObjectFieldTargetAttribute>() is REObjectFieldTargetAttribute overrideAttr) {
                    curclass = overrideAttr.Classname;
                }
                if (curclass == null) continue;

                var games = supportedGames.Length == 0 ? ReachForGodot.GameList : supportedGames;

                foreach (var game in games) {
                    // var config = ReachForGodot.GetAssetConfig(game);
                    // if (!config.IsValid) continue;
                    // // var cache = config.Workspace;
                    if (!fieldOverrides.TryGetValue((game, curclass), out var pergame)) {
                        fieldOverrides[(game, curclass)] = pergame = new();
                    }
                    pergame.Add(accessor);
                }
            }
        }
    }

    private static void RszFieldToGodotProperty(RszField srcField, REField refield, Workspace cache)
    {
        RszFieldToGodotProperty(refield, cache, srcField.type, srcField.array, srcField.original_type);
    }

    private static void RszFieldToGodotProperty(REField refield, Workspace cache, RszFieldType type, bool array, string classname)
    {
        static void ResourceHint(REField field, string resourceName)
        {
            field.VariantType = Variant.Type.Object;
            field.Hint = PropertyHint.ResourceType;
            field.HintString = resourceName;
        }

        if (array) {
            switch (type) {
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
                    refield.VariantType = Variant.Type.Array;
                    refield.Hint = PropertyHint.TypeString;
                    refield.HintString = $"{(int)Variant.Type.Int}/0:";
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
                case RszFieldType.RuntimeType:
                    refield.VariantType = Variant.Type.PackedStringArray;
                    return;
                case RszFieldType.Object:
                    refield.VariantType = Variant.Type.Array;
                    refield.Hint = PropertyHint.TypeString;
                    refield.HintString = $"{(int)Variant.Type.Object}/{(int)PropertyHint.ResourceType}:{nameof(REObject)}";
                    return;
                case RszFieldType.UserData:
                    refield.VariantType = Variant.Type.Array;
                    refield.Hint = PropertyHint.TypeString;
                    refield.HintString = $"{(int)Variant.Type.Object}/{(int)PropertyHint.ResourceType}:{nameof(UserdataResource)}";
                    return;
                case RszFieldType.Resource:
                    refield.VariantType = Variant.Type.Array;
                    refield.Hint = PropertyHint.TypeString;
                    refield.HintString = $"{(int)Variant.Type.Object}/{(int)PropertyHint.ResourceType}:{GetResourceType(refield.RszField.original_type)}";
                    return;
                case RszFieldType.Data:
                    refield.VariantType = Variant.Type.Array;
                    refield.Hint = PropertyHint.TypeString;
                    refield.HintString = $"{(int)Variant.Type.PackedByteArray}/0:";
                    return;
                default:
                    refield.VariantType = Variant.Type.Array;
                    return;
            }
        }

        switch (type) {
            case RszFieldType.Struct:
            case RszFieldType.Object:
                ResourceHint(refield, nameof(REObject));
                break;
            case RszFieldType.UserData:
                ResourceHint(refield, nameof(UserdataResource));
                break;
            case RszFieldType.S8:
            case RszFieldType.S16:
            case RszFieldType.S32:
            case RszFieldType.S64:
            case RszFieldType.U8:
            case RszFieldType.U16:
            case RszFieldType.U32:
            case RszFieldType.U64:
            case RszFieldType.Enum:
                refield.VariantType = Variant.Type.Int;
                if (!string.IsNullOrEmpty(classname)) {
                    var desc = cache.TypeCache.GetEnumDescriptor(classname);
                    if (desc != null && !desc.IsEmpty) {
                        // use Enum and not EnumSuggestion so we could still add custom values
                        refield.Hint = desc.IsFlags ? PropertyHint.Flags : PropertyHint.Enum;
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
                refield.VariantType = Variant.Type.String;
                break;
            case RszFieldType.Uri:
                if (classname.Contains("via.GameObjectRef")) {
                    ResourceHint(refield, nameof(GameObjectRef));
                } else {
                    refield.VariantType = Variant.Type.String;
                }
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
                ResourceHint(refield, GetResourceType(refield.RszField.original_type));
                break;
            default:
                refield.VariantType = Variant.Type.Nil;
                GD.Print("Unhandled rsz field type " + type + " / " + classname);
                break;
        }
    }

    public static string GetEnumHintString(SupportedGame game, string clasname)
    {
        return GetWorkspace(game).TypeCache.GetEnumDescriptor(clasname).HintstringLabels;
    }

    public static string GetEnumLabel<T>(SupportedGame game, string clasname, T id) where T : struct, IBinaryInteger<T>
    {
        var env = GetWorkspace(game);
        var desc = env.TypeCache.GetEnumDescriptor(clasname) as EnumDescriptor<T>;
        if (desc != null && desc.ValueToLabels.TryGetValue(id, out var label)) {
            return label;
        }
        return id.ToString() ?? string.Empty;
    }
}
