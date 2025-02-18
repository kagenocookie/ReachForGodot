namespace RGE;

using System;
using Godot;
using Godot.Collections;
using RszTool;

[GlobalClass, Tool]
public partial class REObject : Resource
{
    [Export] public SupportedGame Game { get; set; }
    [Export] public string? Classname { get; set; }
    [Export] protected Godot.Collections.Dictionary<StringName, Variant> __Data = new();

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

    public REObject(SupportedGame game, string classname, RszInstance instance)
    {
        Game = game;
        Classname = classname;
        ResourceName = classname;
        LoadProperties(instance);
    }

    public override void _ValidateProperty(Dictionary property)
    {
        if (property["name"].AsStringName() == PropertyName.__Data) {
            property["usage"] = (int)(PropertyUsageFlags.Storage|PropertyUsageFlags.ScriptVariable);
        }
        base._ValidateProperty(property);
    }

    public void Rebuild(string classname, RszInstance instance)
    {
        Classname = classname;
        LoadProperties(instance);
    }

    public void LoadProperties(RszInstance instance)
    {
        cache ??= TypeCache.GetData(Game, Classname ?? throw new Exception("Missing REObject classname"));
        foreach (var field in cache.Fields) {
            var value = instance.Values[field.FieldIndex];
            __Data[field.SerializedName] = RszTypeConverter.FromRszValue(field, value, Game);
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
