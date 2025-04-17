namespace ReaGE;

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Godot;
using Godot.Collections;
using RszTool;

[GlobalClass, Tool]
public partial class REObject : Resource
{
    [Export] public SupportedGame Game { get; set; }
    protected string? _classname;

    /// <summary>
    /// The classname that this REObject represents. The setter should be used only during initialization and otherwise changed with ChangeClassname to ensure everything is setup properly.
    /// </summary>
    [Export]
    public string? Classname {
        get => _classname;
        set {
            if (_classname != value) {
                if (!string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(_classname) && Game != SupportedGame.Unknown) {
                    _classname = value;
                    NotifyPropertyListChanged();
                } else {
                    _classname = value;
                }
            }
        }
    }

    public string? ClassBaseName => _classname?.Substring(_classname.LastIndexOf('.') + 1);

    [Export] protected Godot.Collections.Dictionary<StringName, Variant> __Data = new();

    public bool IsEmpty => __Data.Count == 0;
    public bool IsValid => Game != SupportedGame.Unknown && !string.IsNullOrEmpty(Classname) && (cache != null || TypeCache.ClassExists(Game, Classname));
    public ClassInfo TypeInfo => cache ??= SetupTypeInfo(Classname ?? throw new Exception("Missing classname at " + ResourcePath));

    private static readonly List<string> EmptyStringList = new(0);
    private List<string> subclasses = EmptyStringList;
    public List<string> AllowedSubclasses => subclasses;

    private ClassInfo? cache;

    public REObject()
    {
    }

    public REObject(SupportedGame game, string? classname)
    {
        Game = game;
        _classname = classname;
        UpdateResourceName();
    }

    public REObject(SupportedGame game, string classname, bool initializeImmediately)
    {
        Game = game;
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
        if (cache != null && cache.RszClass.name != Classname) {
            cache = null;
        }
        cache ??= SetupTypeInfo(Classname ?? throw new Exception("Missing REObject classname"));
        __Data.Clear();
        foreach (var field in cache.Fields) {
            if (field.RszField.type == RszFieldType.Object) {
                var obj = new REObject(Game, field.RszField.original_type);
                obj.ResetProperties();
                __Data[field.SerializedName] = obj;
            } else if (field.RszField.type is RszFieldType.Resource or RszFieldType.UserData) {
                __Data[field.SerializedName] = new Variant();
            } else {
                var newValue = field.RszField.array ? new List<object>() : RszInstance.CreateNormalObject(field.RszField);
                __Data[field.SerializedName] = RszTypeConverter.FromRszValue(field, newValue, Game);
            }
        }
    }

    public REObject DeepClone() => ((REObject)Activator.CreateInstance(GetType())!).DeepCopyFrom<REObject>(this);

    public TObj DeepClone<TObj>() where TObj : REObject, new()
    {
        return new TObj().DeepCopyFrom<TObj>(this);
    }

    protected TObj DeepCopyFrom<TObj>(REObject from) where TObj : REObject, new()
    {
        ResourceName = from.ResourceName;
        Game = from.Game;
        Classname = from.Classname;
        foreach (var (key, value) in from.__Data) {
            if (value.VariantType == Variant.Type.Object) {
                if (value.As<Resource>() is REObject fieldObj) {
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

    public void ShallowCopyFrom(REObject source, params string[] fields)
    {
        Game = source.Game;
        _classname = source.Classname;
        __Data.Clear();
        cache = SetupTypeInfo(Classname ?? throw new Exception("Missing REObject classname"));
        foreach (var field in cache.Fields) {
            if (fields != null && fields.Length > 0 && !fields.Contains(field.SerializedName)) {
                continue;
            }
            if (source.__Data.TryGetValue(field.SerializedName, out var srcVal)) {
                if (srcVal.VariantType == Variant.Type.Object && srcVal.As<REObject>() is REObject) {
                    __Data[field.SerializedName] = srcVal;
                } else {
                    __Data[field.SerializedName] = Variant.CreateTakingOwnershipOfDisposableValue(srcVal.CopyNativeVariant());
                }
            } else {
                __Data[field.SerializedName] = new Variant();
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

    private static readonly char[] PropertyPathChars = { '.', '/', };
    public (REObject obj, string field, int index) GetFieldByPath(ReadOnlySpan<char> valuePath)
    {
        var dotOrSlash = valuePath.IndexOfAny(PropertyPathChars);
        if (dotOrSlash == -1) {
            return (this, valuePath.ToString(), -1);
        }

        var field = valuePath.Slice(0, dotOrSlash).ToString();
        if (valuePath[dotOrSlash] == '.') {
            var slashpos = valuePath.IndexOf('/');
            var index = int.Parse(
                slashpos == -1 ? valuePath.Slice(dotOrSlash + 1) : valuePath[(dotOrSlash + 1)..slashpos],
                CultureInfo.InvariantCulture);

            if (index < 0) {
                // invalid index
                return default;
            }

            if (!__Data.TryGetValue(field, out var arr) || arr.VariantType != Variant.Type.Array || arr.AsGodotArray() is not Godot.Collections.Array array) {
                if (!TypeInfo.FieldsByName.TryGetValue(field, out var fff) || !fff.RszField.array) {
                    GD.PrintErr($"Invalid array path - {Classname} field {field} is not an array");
                    return default;
                }
                __Data[field] = array = new Godot.Collections.Array();
            }
            if (slashpos == -1) {
                return (this, field, index);
            }
            if (index < array.Count && array[index].As<REObject>() is REObject child) {
                // nested object array field
                return child.GetFieldByPath(valuePath.Slice(slashpos + 1));
            }
        } else {
            // nested plain field
            if (__Data.TryGetValue(field, out var sub) && sub.As<REObject>() is REObject subObj) {
                return subObj.GetFieldByPath(valuePath.Slice(dotOrSlash + 1));
            }
        }
        return default;
    }

    public void SetFieldByPath(ReadOnlySpan<char> valuePath, Variant value)
    {
        var (target, field, index) = GetFieldByPath(valuePath);
        if (target != null) {
            if (index == -1) {
                target.__Data[field] = value;
            } else {
                var array = target.__Data[field].AsGodotArray();
                while (array.Count <= index) {
                    array.Add(new Variant());
                }
                array[index] = value;
            }
        }
    }

    public override Array<Dictionary> _GetPropertyList()
    {
        if (string.IsNullOrWhiteSpace(Classname) || Game == SupportedGame.Unknown) {
            return base._GetPropertyList();
        }

        cache ??= SetupTypeInfo(Classname);
        return cache.PropertyList;
    }

    public void SetBaseClass(string baseclass)
    {
        if (string.IsNullOrEmpty(baseclass)) {
            subclasses = EmptyStringList;
            return;
        }
        subclasses = TypeCache.GetSubclasses(Game, baseclass);
    }

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
        if (cache.FieldsByName.TryGetValue(property, out var field)) {
            __Data[property] = value;
            if (field.RszField.array) {
                if (field.RszField.type == RszFieldType.Object) {
                    var array = value.AsGodotArray<REObject>();
                    foreach (var item in array) {
                        if (item == null) continue;
                        if (item.Game == SupportedGame.Unknown) {
                            item.Game = Game;
                        }
                        if (field.ElementType != null && string.IsNullOrEmpty(item.Classname)) {
                            item.ChangeClassname(field.ElementType);
                        }
                    }
                } else if (field.RszField.type == RszFieldType.UserData) {
                    var array = value.AsGodotArray<UserdataResource>();
                    foreach (var item in array) {
                        if (item == null) continue;
                        if (item.Game == SupportedGame.Unknown) {
                            item.Game = Game;
                        }
                        if (field.ElementType != null && string.IsNullOrEmpty(item.Classname)) {
                            if (item is UserdataResource) {
                                array[array.IndexOf(item)] = new UserdataResource() { Data = new REObject(Game, field.ElementType, true) };
                            }
                        }
                    }
                }
            } else {
                if (field.RszField.type is RszFieldType.Object && value.As<REObject>() is REObject obj) {
                    if (obj.Game == SupportedGame.Unknown) {
                        obj.Game = Game;
                    }
                    if (string.IsNullOrEmpty(obj.Classname) && !string.IsNullOrEmpty(field.RszField.original_type)) {
                        obj.ChangeClassname(field.RszField.original_type);
                        __Data[property] = new REObject(Game, field.RszField.original_type, true);
                    }
                }
                if (field.RszField.type is RszFieldType.UserData && value.As<UserdataResource>() is UserdataResource user) {
                    if (user.Game == SupportedGame.Unknown) {
                        user.Game = Game;
                    }
                }
            }
            return true;
        }
        return base._Set(property, value);
    }

    protected ClassInfo SetupTypeInfo(string cls)
    {
        cache = TypeCache.GetClassInfo(Game, cls);
        UpdateFieldTypes();
        return cache;
    }

    public void UpdateFieldTypes()
    {
        cache ??= TypeCache.GetClassInfo(Game, Classname!);
        foreach (var (key, value) in __Data) {
            if (value.VariantType == Variant.Type.Object) {
                if (cache.FieldsByName.TryGetValue(key, out var field) && field.RszField.type is RszFieldType.Object or RszFieldType.UserData && value.As<Resource>() is Resource res) {
                    if (res is REObject fieldObj) {
                        fieldObj.SetBaseClass(field.RszField.original_type);
                    } else if (res is UserdataResource ur) {
                        ur.Data.SetBaseClass(field.RszField.original_type);
                    }
                }
            } else if (value.VariantType == Variant.Type.Array) {
                if (cache.FieldsByName.TryGetValue(key, out var field) && field.RszField.type is RszFieldType.Object or RszFieldType.UserData) {
                    if (field.ElementType == null) continue;
                    var array = value.AsGodotArray();
                    foreach (var item in array) {
                        if (item.VariantType != Variant.Type.Nil && item.As<Resource>() is Resource res) {
                            if (res is REObject itemObj) {
                                itemObj.SetBaseClass(field.ElementType);
                            } else if (res is UserdataResource user) {
                                user.Data.SetBaseClass(field.ElementType);
                            }
                        }
                    }
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetFieldValue(REField field, out Variant variant)
    {
        return __Data.TryGetValue(field.SerializedName, out variant);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetFieldValue(REFieldAccessor field, out Variant variant)
    {
        return __Data.TryGetValue(field.Get(this).SerializedName, out variant);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Variant GetField(REField field)
    {
        return __Data.TryGetValue(field.SerializedName, out var variant) ? variant : new Variant();
    }

    public Variant GetField(int fieldIndex) => GetField(TypeInfo.Fields[fieldIndex]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Variant GetField(REFieldAccessor field)
    {
        return __Data.TryGetValue(field.Get(this).SerializedName, out var variant) ? variant : new Variant();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetField(REField field, Variant value)
    {
        __Data[field.SerializedName] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetField(REFieldAccessor field, Variant value)
    {
        __Data[field.Get(this).SerializedName] = value;
    }

    public void SetField(string field, Variant value)
    {
        if (TypeInfo.FieldsByName.TryGetValue(field, out var fieldRef)) {
            __Data[fieldRef.SerializedName] = value;
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
