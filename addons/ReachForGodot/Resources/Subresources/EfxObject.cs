namespace ReaGE;

using System;
using System.Runtime.CompilerServices;
using Godot;
using Godot.Collections;
using RszTool;
using RszTool.Efx;

[GlobalClass, Tool]
public partial class EfxObject : Resource
{
    [Export] public EfxVersion Version { get; set; }
    protected string? _classname;

    /// <summary>
    /// The classname that this REObject represents. The setter should be used only during initialization and otherwise changed with ChangeClassname to ensure everything is setup properly.
    /// </summary>
    [Export]
    public string? Classname {
        get => _classname;
        set {
            if (_classname != value) {
                if (!string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(_classname) && Version != EfxVersion.Unknown) {
                    _classname = value;
                    NotifyPropertyListChanged();
                } else {
                    _classname = value;
                }
            }
        }
    }

    public string? ClassBaseName
    {
        get {
            var cls = _classname?.Substring(_classname.LastIndexOf('.') + 1);
            if (cls?.StartsWith("EFXAttribute") == true) return cls.Replace("EFXAttribute", "");
            return cls;
        }
    }

    [Export] protected Godot.Collections.Dictionary<StringName, Variant> __Data = new();

    public bool IsEmpty => __Data.Count == 0;
    public bool IsValid => Version != EfxVersion.Unknown && !string.IsNullOrEmpty(Classname) && (cache != null || TypeCache.EfxStructExists(Version, Classname));
    public EfxClassInfo TypeInfo => cache ??= SetupTypeInfo(Classname ?? throw new Exception("Missing classname at " + ResourcePath));

    private static readonly List<string> EmptyStringList = new(0);
    private List<string> subclasses = EmptyStringList;
    public List<string> AllowedSubclasses => subclasses;

    private EfxClassInfo? cache;

    public EfxObject()
    {
    }

    public EfxObject(EfxVersion game, string? classname)
    {
        Version = game;
        _classname = classname;
        UpdateResourceName();
    }

    public EfxObject(EfxVersion game, EfxClassInfo cacheInfo)
    {
        Version = game;
        _classname = cacheInfo.Info.Classname;
        this.cache = cacheInfo;
        UpdateResourceName();
    }

    public EfxObject(EfxVersion game, string classname, bool initializeImmediately)
    {
        Version = game;
        _classname = classname;
        UpdateResourceName();
        if (initializeImmediately) ResetProperties();
    }

    public void ChangeClassname(string newClassname)
    {
        if (string.IsNullOrEmpty(newClassname)) {
            _classname = newClassname;
            return;
        }

        if (newClassname == _classname) return;

        _classname = newClassname;
        ResetProperties();
        UpdateResourceName();
        NotifyPropertyListChanged();
    }

    protected virtual void UpdateResourceName()
    {
        ResourceName = ClassBaseName;
    }

    public override void _ValidateProperty(Dictionary property)
    {
        if (property["name"].AsStringName() == PropertyName.__Data) {
            property["usage"] = (int)(PropertyUsageFlags.Storage | PropertyUsageFlags.ScriptVariable);
        }
        base._ValidateProperty(property);
    }

    public void ResetProperties()
    {
        if (cache != null && cache.Info.Classname != Classname) {
            cache = null;
        }
        cache ??= SetupTypeInfo(Classname ?? throw new Exception("Missing REObject classname"));
        __Data.Clear();
        foreach (var field in cache.Info.Fields) {
            if (field.FieldType == RszFieldType.Object || field.FieldType == RszFieldType.Struct) {
                var obj = new EfxObject(Version, field.Classname);
                obj.ResetProperties();
                __Data[field.Name] = obj;
            } else {
                // var newValue = field.RszField.array ? new List<object>() : RszInstance.CreateNormalObject(field.RszField);
                // __Data[field.Name] = RszTypeConverter.FromRszValue(field, newValue, Version);
            }
        }
    }

    public EfxObject DeepClone() => ((EfxObject)Activator.CreateInstance(GetType())!).DeepCopyFrom<EfxObject>(this);

    public TObj DeepClone<TObj>() where TObj : EfxObject, new()
    {
        return new TObj().DeepCopyFrom<TObj>(this);
    }

    protected TObj DeepCopyFrom<TObj>(EfxObject from) where TObj : EfxObject, new()
    {
        ResourceName = from.ResourceName;
        Version = from.Version;
        Classname = from.Classname;
        foreach (var (key, value) in from.__Data) {
            if (value.VariantType == Variant.Type.Object) {
                if (value.As<Resource>() is EfxObject fieldObj) {
                    __Data[key] = fieldObj.DeepClone();
                } else {
                    __Data[key] = value;
                }
            } else if (value.VariantType == Variant.Type.Array) {
                if (value.AsGodotArray() is Godot.Collections.Array array) {
                    var newArray = new Godot.Collections.Array();
                    foreach (var item in array) {
                        if (item.VariantType == Variant.Type.Object && item.As<Resource>() is REObject itemObj) {
                            newArray.Add(itemObj.DeepClone());
                        } else {
                            newArray.Add(Variant.CreateTakingOwnershipOfDisposableValue(item.CopyNativeVariant()));
                        }
                    }
                    __Data[key] = newArray;
                } else {
                    __Data[key] = new Variant();
                }
            } else {
                __Data[key] = Variant.CreateTakingOwnershipOfDisposableValue(value.CopyNativeVariant());
            }
        }
        return (TObj)this;
    }

    public void ShallowCopyFrom(EfxObject source, params string[] fields)
    {
        Version = source.Version;
        _classname = source.Classname;
        __Data.Clear();
        cache = SetupTypeInfo(Classname ?? throw new Exception("Missing EfxObject classname"));
        foreach (var (name, field) in cache.Fields) {
            if (fields != null && fields.Length > 0 && !fields.Contains(name)) {
                continue;
            }
            if (source.__Data.TryGetValue(name, out var srcVal)) {
                if (srcVal.VariantType == Variant.Type.Object && srcVal.As<EfxObject>() is EfxObject) {
                    __Data[name] = srcVal;
                } else {
                    __Data[name] = Variant.CreateTakingOwnershipOfDisposableValue(srcVal.CopyNativeVariant());
                }
            } else {
                __Data[name] = new Variant();
            }
        }
    }

    public void CollectResources(HashSet<REResource> list)
    {
        foreach (var res in NestedResources()) {
            list.Add(res);
        }
    }

    public IEnumerable<(StringName, int, REObject)> ChildObjects()
    {
        foreach (var (key, value) in __Data) {
            if (value.VariantType == Variant.Type.Object && value.As<Resource>() is REObject obj) {
                yield return (key, -1, obj);
            } else if (value.VariantType == Variant.Type.Array) {
                var array = value.AsGodotArray();
                for (var i = 0; i < array.Count; i++) {
                    Variant item = array[i];
                    if (item.VariantType == Variant.Type.Object && item.As<Resource>() is REObject obj2) {
                        yield return (key, i, obj2);
                    }
                }
            }
        }
    }

    public IEnumerable<(string, Resource)> GetEngineObjectsWithPaths(string? path = null)
    {
        foreach (var (field, i, res) in Resources()) {
            var fieldkey = i == -1 ? field.ToString() : $"{field}.{i}";
            var fullpath = path == null ? fieldkey : path + "/" + fieldkey;
            yield return (fullpath, res);
        }

        foreach (var (field, i, obj) in ChildObjects()) {
            var fieldkey = i == -1 ? field.ToString() : $"{field}.{i}";
            var fullpath = path == null ? fieldkey : path + "/" + fieldkey;
            yield return (fullpath, obj);

            foreach (var subresult in obj.GetEngineObjectsWithPaths(fullpath)) {
                yield return subresult;
            }
        }
    }

    public IEnumerable<(StringName, int, REResource)> Resources()
    {
        foreach (var (key, value) in __Data) {
            if (value.VariantType == Variant.Type.Object && value.As<Resource>() is REResource obj) {
                yield return (key, -1, obj);
            } else if (value.VariantType == Variant.Type.Array) {
                var array = value.AsGodotArray();
                for (var i = 0; i < array.Count; i++) {
                    Variant item = array[i];
                    if (item.VariantType == Variant.Type.Object && item.As<Resource>() is REResource obj2) {
                        yield return (key, i, obj2);
                    }
                }
            }
        }
    }

    public IEnumerable<REResource> NestedResources()
    {
        foreach (var res in Resources()) {
            yield return res.Item3;
        }

        foreach (var childres in ChildObjects().SelectMany(o => o.Item3.NestedResources())) {
            yield return childres;
        }
    }

    public void ClearResources()
    {
        foreach (var (key, index, res) in Resources()) {
            __Data[key] = new Variant();
        }

        foreach (var obj in ChildObjects()) {
            obj.Item3.ClearResources();
        }
    }

    public void ApplyResources(Dictionary<string, string> fields)
    {
        foreach (var (key, index, res) in Resources()) {
            __Data[key] = new Variant();
        }

        foreach (var obj in ChildObjects()) {
            obj.Item3.ApplyResources(fields);
        }
    }

    public override Array<Dictionary> _GetPropertyList()
    {
        if (string.IsNullOrWhiteSpace(Classname) || Version == EfxVersion.Unknown) {
            return base._GetPropertyList();
        }

        cache ??= SetupTypeInfo(Classname);
        return cache.PropertyList;
    }

    // public void SetBaseClass(string baseclass)
    // {
    //     if (string.IsNullOrEmpty(baseclass)) {
    //         subclasses = EmptyStringList;
    //         return;
    //     }
    //     subclasses = TypeCache.GetSubclasses(Version, baseclass);
    // }

    public override Variant _Get(StringName property)
    {
        if (string.IsNullOrWhiteSpace(Classname)) {
            return default;
        }

        if (__Data.TryGetValue(property, out var val)) {
            return val;
        }

        return default;
    }

    public override bool _Set(StringName property, Variant value)
    {
        if (string.IsNullOrWhiteSpace(Classname)) {
            return false;
        }

        cache ??= SetupTypeInfo(Classname);
        if (cache.Fields.TryGetValue(property, out var field)) {
            __Data[property] = value;
            return true;
        }
        return base._Set(property, value);
    }

    protected EfxClassInfo SetupTypeInfo(string cls)
    {
        cache = TypeCache.GetEfxStructInfo(Version, cls);
        return cache;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetFieldValue(EfxFieldInfo field, out Variant variant)
    {
        return __Data.TryGetValue(field.Name, out variant);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Variant GetField(EfxFieldInfo field)
    {
        return __Data.TryGetValue(field.Name, out var variant) ? variant : new Variant();
    }

    public Variant GetField(int fieldIndex) => GetField(TypeInfo.Info.Fields[fieldIndex]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetField(EfxFieldInfo field, Variant value)
    {
        __Data[field.Name] = value;
    }

    public void SetField(string field, Variant value)
    {
        if (TypeInfo.Fields.TryGetValue(field, out var fieldRef)) {
            __Data[fieldRef.Name] = value;
        } else {
            GD.PrintErr($"Could not set unknown field {field} for class {Classname}");
        }
    }

    protected Dictionary CreatePropertyCategory(string name, string? hintstring = null)
    {
        var dict = new Godot.Collections.Dictionary()
        {
            { "name", name },
            { "type", (int)Variant.Type.Nil },
            { "usage", (int)(PropertyUsageFlags.Category|PropertyUsageFlags.ScriptVariable) }
        };
        if (hintstring != null) dict["hint_string"] = hintstring;
        return dict;
    }

    protected Dictionary CreateProperty(string name, Variant.Type type, PropertyHint hint = PropertyHint.None, string? hintstring = null)
    {
        var dict = new Godot.Collections.Dictionary()
        {
            { "name", name },
            { "type", (int)type },
            { "hint", (int)hint },
            { "usage", (int)(PropertyUsageFlags.Default|PropertyUsageFlags.Storage|PropertyUsageFlags.ScriptVariable) }
        };
        if (hintstring != null) dict["hint_string"] = hintstring;
        return dict;
    }

    public override string ToString() => !string.IsNullOrEmpty(Classname) ? Classname : !string.IsNullOrEmpty(ResourceName) ? ResourceName : "REObject";
}
