namespace ReaGE;

using System;
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
    public bool IsValid => Game != SupportedGame.Unknown && !string.IsNullOrEmpty(Classname) && TypeInfo != null;
    public REObjectTypeCache TypeInfo => cache ??= TypeCache.GetData(Game, Classname ?? throw new Exception("Missing classname at " + ResourcePath));

    private string[] subclasses = System.Array.Empty<string>();
    public string[] AllowedSubclasses => subclasses;

    private REObjectTypeCache? cache;

    public REObject()
    {
    }

    public REObject(SupportedGame game, string classname)
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
        cache ??= TypeCache.GetData(Game, Classname ?? throw new Exception("Missing REObject classname"));
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

    public void ShallowCopyFrom(REObject source, params string[] fields)
    {
        Game = source.Game;
        _classname = source.Classname;
        __Data.Clear();
        cache = TypeCache.GetData(Game, Classname ?? throw new Exception("Missing REObject classname"));
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

    public override Array<Dictionary> _GetPropertyList()
    {
        if (string.IsNullOrWhiteSpace(Classname)) {
            return base._GetPropertyList();
        }

        if (cache == null) {
            // note, ideally we would prefer to avoid doing any of this during save, but godot calls it either way and we have no way of knowing which one it is
            cache = TypeCache.GetData(Game, Classname);
            foreach (var (key, value) in __Data) {
                if (value.VariantType == Variant.Type.Object) {
                    if (cache.FieldsByName.TryGetValue(key, out var field) && field.RszField.type is RszFieldType.Object or RszFieldType.UserData) {
                        if (value.As<REObject>() is not REObject fieldObj) continue;
                        if (fieldObj is UserdataResource ur && ur.Asset?.IsEmpty == false) continue;
                        fieldObj.SetBaseClass(field.RszField.original_type);
                    }
                } else if (value.VariantType == Variant.Type.Array) {
                    if (cache.FieldsByName.TryGetValue(key, out var field) && field.RszField.type is RszFieldType.Object or RszFieldType.UserData) {
                        if (field.ElementType == null) continue;
                        var array = value.AsGodotArray();
                        foreach (var item in array) {
                            if (item.VariantType != Variant.Type.Nil && item.As<REObject>() is REObject itemObj) {
                                if (itemObj is UserdataResource ur && ur.Asset?.IsEmpty == false) continue;
                                itemObj.SetBaseClass(field.ElementType);
                            }
                        }
                    }
                }
            }
        }
        return cache.PropertyList;
    }

    private void SetBaseClass(string baseclass)
    {
        if (string.IsNullOrEmpty(baseclass)) {
            subclasses = System.Array.Empty<string>();
            return;
        }
        // TODO properly evaluate object subclasses
        subclasses = new[] { baseclass };
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

        cache ??= TypeCache.GetData(Game, Classname);
        if (cache.FieldsByName.TryGetValue(property, out var field)) {
            __Data[property] = value;
            if (field.RszField.array) {
                if (field.ElementType != null) {
                    var array = value.AsGodotArray<REObject>();
                    foreach (var item in array) {
                        if (item == null) continue;
                        if (item.Game == SupportedGame.Unknown) {
                            item.Game = Game;
                        }
                        if (string.IsNullOrEmpty(item.Classname)) {
                            if (item is UserdataResource) {
                                array[array.IndexOf(item)] = new REObject(Game, field.ElementType, true);
                            } else {
                                item.ChangeClassname(field.ElementType);
                            }
                        }
                    }
                }
            } else {
                if (field.RszField.type is RszFieldType.Object or RszFieldType.UserData && value.As<REObject>() is REObject obj) {
                    if (obj.Game == SupportedGame.Unknown) {
                        obj.Game = Game;
                    }
                    if (string.IsNullOrEmpty(obj.Classname) && !string.IsNullOrEmpty(field.RszField.original_type)) {
                        if (obj is UserdataResource) {
                            __Data[property] = new REObject(Game, field.RszField.original_type, true);
                        } else {
                            obj.ChangeClassname(field.RszField.original_type);
                        }
                    }
                }
            }
            return true;
        }
        return base._Set(property, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetFieldValue(REField field, out Variant variant)
    {
        return __Data.TryGetValue(field.SerializedName, out variant);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetFieldValue(REObjectFieldAccessor field, out Variant variant)
    {
        return __Data.TryGetValue(field.Get(this).SerializedName, out variant);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Variant GetField(REField field)
    {
        return __Data.TryGetValue(field.SerializedName, out var variant) ? variant : new Variant();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Variant GetField(REObjectFieldAccessor field)
    {
        return __Data.TryGetValue(field.Get(this).SerializedName, out var variant) ? variant : new Variant();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetField(REField field, Variant value)
    {
        __Data[field.SerializedName] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetField(REObjectFieldAccessor field, Variant value)
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
