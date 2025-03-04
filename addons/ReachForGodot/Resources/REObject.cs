namespace RGE;

using System;
using System.Runtime.CompilerServices;
using Godot;
using Godot.Collections;
using RszTool;

[GlobalClass, Tool]
public partial class REObject : Resource
{
    [Export] public SupportedGame Game { get; set; }
    [Export] public string? Classname { get; set; }
    [Export] protected Godot.Collections.Dictionary<StringName, Variant> __Data = new();

    public bool IsEmpty => __Data.Count == 0;
    public REObjectTypeCache TypeInfo => cache ??= TypeCache.GetData(Game, Classname ?? throw new Exception("Missing classname at " + ResourcePath));

    private REObjectTypeCache? cache;

    public REObject()
    {
    }

    public REObject(SupportedGame game, string classname)
    {
        Game = game;
        Classname = classname;
        ResourceName = classname;
    }

    public override void _ValidateProperty(Dictionary property)
    {
        if (property["name"].AsStringName() == PropertyName.__Data) {
            property["usage"] = (int)(PropertyUsageFlags.Storage|PropertyUsageFlags.ScriptVariable);
        }
        base._ValidateProperty(property);
    }

    public void ResetProperties()
    {
        cache ??= TypeCache.GetData(Game, Classname ?? throw new Exception("Missing REObject classname"));
        foreach (var field in cache.Fields) {
            __Data[field.SerializedName] = RszTypeConverter.FromRszValue(field, RszInstance.CreateNormalObject(field.RszField), Game);
        }
    }

    public void CloneFrom(REObject source)
    {
        Game = source.Game;
        Classname = source.Classname;
        cache = TypeCache.GetData(Game, Classname ?? throw new Exception("Missing REObject classname"));
        foreach (var field in cache.Fields) {
            if (source.__Data.TryGetValue(field.SerializedName, out var srcVal)) {
                __Data[field.SerializedName] = Variant.CreateTakingOwnershipOfDisposableValue(srcVal.CopyNativeVariant());
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

        cache ??= TypeCache.GetData(Game, Classname);
        return cache.PropertyList;
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
