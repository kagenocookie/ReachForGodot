namespace ReaGE;

using System;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using RszTool;

public static partial class TypeCache
{
    static TypeCache()
    {
        System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(typeof(TypeCache).Assembly)!.Unloading += (c) => {
            var assembly = typeof(System.Text.Json.JsonSerializerOptions).Assembly;
            var updateHandlerType = assembly.GetType("System.Text.Json.JsonSerializerOptionsUpdateHandler");
            var clearCacheMethod = updateHandlerType?.GetMethod("ClearCache", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            clearCacheMethod!.Invoke(null, new object?[] { null });

            allCacheData.Clear();
        };
        InitResourceFormats(typeof(TypeCache).Assembly);
        InitComponents(typeof(TypeCache).Assembly);
        jsonOptions.Converters.Add(new JsonStringEnumConverter<SupportedFileFormats>(allowIntegerValues: false));
        jsonOptions.Converters.Add(new JsonStringEnumConverter<RszFieldType>(allowIntegerValues: false));
        jsonOptions.Converters.Add(new JsonStringEnumConverter<EfxFieldFlags>(allowIntegerValues: false));
    }

    private static readonly Dictionary<SupportedGame, GameClassCache> allCacheData = new();

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

    public static RszFileOption CreateRszFileOptions(AssetConfig config)
    {
        _ = GetCacheRoot(config.Game).Parser;
        return new RszFileOption(
            config.Paths.GetRszToolGameEnum(),
            config.Paths.RszJsonPath ?? throw new Exception("Rsz json file not specified for game " + config.Game));
    }

    public static ClassInfo GetClassInfo(SupportedGame game, string classname)
    {
        var cache = GetCacheRoot(game);

        if (!cache.serializationCache.TryGetValue(classname, out var data)) {
            var cls = cache.Parser.GetRSZClass(classname);
            if (cls != null) {
                cache.serializationCache[classname] = data = GenerateObjectCache(cache, cls);
            } else {
                cache.serializationCache[classname] = data = ClassInfo.Empty;
            }
        }
        return data;
    }

    public static bool ClassExists(SupportedGame game, string classname)
    {
        return GetCacheRoot(game).Parser.GetRSZClass(classname) != null;
    }

    public static List<string> GetSubclasses(SupportedGame game, string baseclass)
    {
        var cache = GetCacheRoot(game).Il2cppCache;
        if (cache.subclasses.TryGetValue(baseclass, out var list)) {
            return list;
        }

        return cache.subclasses[baseclass] = new List<string>() { baseclass };
    }

    public static void UpdatePfbGameObjectRefCache(SupportedGame game, string classname, Dictionary<string, PrefabGameObjectRefProperty> propInfoDict)
    {
        var cache = GetCacheRoot(game);
        GetClassInfo(game, classname).PfbRefs = propInfoDict;
        cache.UpdateClassProps(classname, propInfoDict);
    }

    public static void StoreInferredRszTypes(RSZFile rsz, AssetConfig config)
    {
        var handled = new HashSet<RszClass>();
        foreach (var inst in rsz.InstanceList) {
            handled.Add(inst.RszClass);
        }

        if (handled.Count > 0) {
            StoreInferredRszTypes(handled, config);
        }
    }

    public static void MarkRSZResourceFields(IEnumerable<(string classname, string field, string resourceExtension)> fields, AssetConfig config)
    {
        var cache = GetCacheRoot(config.Game);
        int changes = 0;
        foreach (var (cls, field, ext) in fields) {
            var typeinfo = cache.Parser.GetRSZClass(cls);
            var rszType = typeinfo?.fields.FirstOrDefault(f => f.name == field);
            if (rszType != null) {
                if (rszType.type is not RszFieldType.String and not RszFieldType.Resource) {
                    GD.PrintErr($"Attempted to change non-string field into resource: {cls} field {field} of type {rszType.type}");
                    continue;
                }
                var fileFormat = PathUtils.GetFileFormatFromExtension(ext);
                cache.TryFindClassPatch(cls, out var patch);
                var fieldPatch = patch?.FieldPatches?.FirstOrDefault(f => f.Name == field);
                var shouldAdd = rszType.type == RszFieldType.String ||
                    fileFormat != SupportedFileFormats.Unknown && fileFormat != fieldPatch?.FileFormat;

                if (shouldAdd) {
                    if (patch == null) {
                        patch = cache.FindOrCreateClassPatch(cls);
                        fieldPatch = patch.FieldPatches?.FirstOrDefault(f => f.Name == field);
                    }
                    if (fieldPatch == null) {
                        fieldPatch = new EnhancedRszFieldPatch() { Name = field };
                        patch.FieldPatches = (patch.FieldPatches ?? Array.Empty<EnhancedRszFieldPatch>()).Append(fieldPatch).ToArray();
                    }
                    if (fileFormat == SupportedFileFormats.Unknown) fileFormat = fieldPatch.FileFormat;
                    if (fieldPatch.FileFormat != SupportedFileFormats.Unknown && fieldPatch.FileFormat != fileFormat) {
                        // handle conflicts
                        if (fileFormat is SupportedFileFormats.Texture or SupportedFileFormats.RenderTexture && fieldPatch.FileFormat is SupportedFileFormats.Texture or SupportedFileFormats.RenderTexture) {
                            fileFormat = SupportedFileFormats.Texture;
                        } else {
                            GD.PrintErr($"Warning: Resource type conflict on field {cls} {field}: {fieldPatch.FileFormat} and {fileFormat}. Manually verify please.");
                        }
                    }
                    fieldPatch.FileFormat = fileFormat;
                    if (fileFormat is SupportedFileFormats.Scene or SupportedFileFormats.Prefab) {
                        // leave these as string
                        fieldPatch.Type = RszFieldType.String;
                    } else {
                        fieldPatch.Type = RszFieldType.Resource;
                    }
                    changes++;
                }
            }
        }

        if (changes > 0) {
            cache.UpdateRszPatches(config);
            GD.Print($"Updating RSZ inferred resource fields in type cache with {changes} changes");
        }
    }

    public static void StoreInferredRszTypes(IEnumerable<RszClass> classlist, AssetConfig config)
    {
        var cache = GetCacheRoot(config.Game);
        int changes = 0;
        foreach (var cls in classlist) {

            foreach (var f in cls.fields) {
                if (!f.IsTypeInferred) continue;
                if (f.type <= RszFieldType.Undefined) {
                    continue;
                }
                var props = cache.FindOrCreateClassPatch(cls.name);

                var entry = props.FieldPatches?.FirstOrDefault(patch => patch.Name == f.name || patch.ReplaceName == f.name);
                if (entry == null) {
                    entry = new EnhancedRszFieldPatch() { Name = f.name, Type = f.type };
                    props.FieldPatches = props.FieldPatches == null ? [entry] : props.FieldPatches.Append(entry).ToArray();
                    changes++;
                    RebuildSingleFieldCache(cache, cls.name, f.name);
                } else if (entry.Type != f.type) {
                    entry.Type = f.type;
                    changes++;
                    RebuildSingleFieldCache(cache, cls.name, f.name);
                }
            }
        }
        if (changes > 0) {
            cache.UpdateRszPatches(config);
            GD.Print($"Updating RSZ inferred field type cache with {changes} changes");
        }
    }

    public static void VerifyDuplicateFields(IEnumerable<RszClass> classlist, AssetConfig config)
    {
        var cache = GetCacheRoot(config.Game);
        var dupeDict = new Dictionary<string, int>();
        int changes = 0;
        RszClassPatch? patch;
        foreach (var cls in classlist) {
            dupeDict.Clear();
            patch = null;
            foreach (var f in cls.fields) {
                if (dupeDict.TryGetValue(f.name, out var count)) {
                    patch ??= cache.FindOrCreateClassPatch(cls.name);
                    if (count == 1) {
                        var prev = cls.fields.First(p => p.name == f.name);
                        var entryPrev = new RszFieldPatch() { Name = f.name, ReplaceName = f.name + "1" };
                        prev.name = entryPrev.ReplaceName;
                        patch.FieldPatches = patch.FieldPatches == null ? [entryPrev] : patch.FieldPatches.Append(entryPrev).ToArray();
                        changes++;
                    }
                    var nameOverride = f.name + (dupeDict[f.name] = ++count);
                    var entry = new RszFieldPatch() { Name = f.name, ReplaceName = nameOverride };
                    patch.FieldPatches = patch.FieldPatches == null ? [entry] : patch.FieldPatches.Append(entry).ToArray();
                    changes++;
                    f.name = nameOverride;
                } else {
                    dupeDict[f.name] = 1;
                }
            }
        }
        if (changes > 0) {
            cache.UpdateRszPatches(config);
            GD.Print($"Updating RSZ duplicate field names for {changes} fields");
        }
    }

    private static GameClassCache GetCacheRoot(SupportedGame game)
    {
        if (!allCacheData.TryGetValue(game, out var data)) {
            allCacheData[game] = data = new(game);
        }
        return data;
    }

    public static EnumDescriptor GetEnumDescriptor(SupportedGame game, string classname)
        => GetEnumDescriptor(GetCacheRoot(game), classname);

    private static EnumDescriptor GetEnumDescriptor(GameClassCache cache, string classname)
    {
        var il2cpp = cache.Il2cppCache;
        if (il2cpp.enums.TryGetValue(classname, out var descriptor)) {
            return descriptor;
        }

        return EnumDescriptor<int>.Default;
    }

    private static ClassInfo GenerateObjectCache(GameClassCache root, RszClass cls)
    {
        var cache = new ClassInfo(cls, GenerateFields(cls, root), root.GetClassProps(cls.name));
        if (root.fieldOverrides.TryGetValue(cls.name, out var list) == true) {
            foreach (var accessor in list) {
                var field = accessor.Get(root.game, cache);
                if (field != null) {
                    accessor.Invoke(field);
                    var prop = cache.PropertyList.First(dict => dict["name"].AsString() == field.SerializedName);
                    if (field.RszField.name != accessor.preferredName) {
                        var props = root.FindOrCreateClassPatch(cls.name);
                        var patch = props.FieldPatches?.FirstOrDefault(fp => fp.Name == field.RszField.name);
                        if (patch == null) {
                            patch = new EnhancedRszFieldPatch() {
                                Name = field.RszField.name,
                                Type = field.RszField.type,
                            };
                            props.FieldPatches = (props.FieldPatches ?? Array.Empty<EnhancedRszFieldPatch>()).Append(patch).ToArray();
                        }
                        patch.ReplaceName = accessor.preferredName;
                        field.RszField.name = accessor.preferredName;
                        root.UpdateRszPatches(ReachForGodot.GetAssetConfig(root.game));
                    }
                    cache.UpdateFieldProperty(field, prop);
                }
            }
        }

        return cache;
    }

    private static REField[] GenerateFields(RszClass cls, GameClassCache cache)
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
                    var cache = GetCacheRoot(game);
                    if (!cache.fieldOverrides.TryGetValue(curclass, out var pergame)) {
                        cache.fieldOverrides[curclass] = pergame = new();
                    }
                    pergame.Add(accessor);
                }
            }
        }
    }

    private static void RszFieldToGodotProperty(RszField srcField, REField refield, GameClassCache cache)
    {
        RszFieldToGodotProperty(refield, cache, srcField.type, srcField.array, srcField.original_type);
    }

    private static void RszFieldToGodotProperty(REField refield, GameClassCache cache, RszFieldType type, bool array, string classname)
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
                    refield.HintString = $"{(int)Variant.Type.Object}/{(int)PropertyHint.ResourceType}:{cache.GetResourceType(refield.RszField.name, refield.SerializedName)}";
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
                    var desc = GetEnumDescriptor(cache, classname);
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
                ResourceHint(refield, cache.GetResourceType(refield.RszField.name, refield.SerializedName));
                break;
            default:
                refield.VariantType = Variant.Type.Nil;
                GD.Print("Unhandled rsz field type " + type + " / " + classname);
                break;
        }
    }

    private static void RebuildSingleFieldCache(GameClassCache cache, string classname, string field)
    {
        if (cache.serializationCache.TryGetValue(classname, out var cls)) {
            var fieldObj = cls.GetFieldByName(field);
            var prop = cls.PropertyList.First(dict => dict["name"].AsString() == field);
            Debug.Assert(fieldObj != null);
            Debug.Assert(prop != null);
            RszFieldToGodotProperty(fieldObj.RszField, fieldObj, cache);
            cls.UpdateFieldProperty(fieldObj, prop);
        }
    }

    public static string GetEnumHintString(SupportedGame game, string clasname)
    {
        return GetEnumDescriptor(GetCacheRoot(game), clasname).HintstringLabels;
    }

    public static string GetEnumLabel<T>(SupportedGame game, string clasname, T id) where T : struct, IBinaryInteger<T>
    {
        var cache = GetCacheRoot(game);
        var desc = GetEnumDescriptor(cache, clasname) as EnumDescriptor<T>;
        if (desc != null && desc.ValueToLabels.TryGetValue(id, out var label)) {
            return label;
        }
        return id.ToString() ?? string.Empty;
    }
}

public class PrefabGameObjectRefProperty
{
    public int PropertyId { get; set; }
    public bool? AutoDetected { get; set; }
}

public class EnhancedRszClassPatch : RszClassPatch
{
    private EnhancedRszFieldPatch[]? _fieldPatches;
    public new EnhancedRszFieldPatch[]? FieldPatches {
        get => _fieldPatches;
        set => base.FieldPatches = _fieldPatches = value;
    }
}

public class EnhancedRszFieldPatch : RszFieldPatch
{
    public SupportedFileFormats FileFormat { get; set; }
}
