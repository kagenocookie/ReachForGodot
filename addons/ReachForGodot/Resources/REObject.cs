namespace RFG;

using System;
using Godot;
using Godot.Collections;
using RszTool;

[GlobalClass, Tool]
public partial class REObject : Resource
{
    [Export] public SupportedGame Game { get; set; }
    [Export] public string? Classname { get; set; }
    Godot.Collections.Dictionary Data = new();

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
        if (property["name"].AsStringName() == PropertyName.Data) {
            property["usage"] = (int)(PropertyUsageFlags.Storage|PropertyUsageFlags.ScriptVariable);
        }
        base._ValidateProperty(property);
    }

    public void LoadProperties(RszInstance instance)
    {
        cache ??= TypeCache.GetData(Game, Classname ?? throw new Exception("Missing REObject classname"));
        foreach (var field in cache.Fields) {
            var value = instance.Values[field.FieldIndex];
            if (field.Hint == PropertyHint.ResourceType) {
                Data[field.SerializedName] = new Variant();
            } else {
                Data[field.SerializedName] = RszTypeConverter.FromRszValue(field, value);
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
            return base._Get(property);
        }

        cache ??= TypeCache.GetData(Game, Classname);
        if (cache.FieldsByName.TryGetValue(property, out var field)) {
            Data.TryGetValue(property, out var val);
            return val;
        }

//           at RFG.REObject._Get(Godot.StringName)
//    at Godot.Bridge.CSharpInstanceBridge.Get(IntPtr, Godot.NativeInterop.godot_string_name*, Godot.NativeInterop.godot_variant*)
//    at Godot.NativeInterop.NativeFuncs.godotsharp_method_bind_ptrcall(IntPtr, IntPtr, Void**, Void*)
//    at Godot.NativeCalls.godot_icall_1_143(IntPtr, IntPtr, Godot.NativeInterop.godot_string_name)
//    at Godot.GodotObject.Get(Godot.StringName)
//    at RFG.REObject._Get(Godot.StringName)
        return base._Get(property);
    }

    public override bool _Set(StringName property, Variant value)
    {
        if (string.IsNullOrWhiteSpace(Classname)) {
            return false;
        }

        cache ??= TypeCache.GetData(Game, Classname);
        if (cache.FieldsByName.TryGetValue(property, out var field)) {
            Data[property] = value;
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
}
