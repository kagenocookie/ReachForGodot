namespace ReaGE;

using System;
using Godot;
using RszTool;

public class ClassInfo
{
    public static readonly ClassInfo Empty = new ClassInfo(RszClass.Empty, Array.Empty<REField>(), new(0));

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

    public ClassInfo(RszClass cls, REField[] fields, Dictionary<string, PrefabGameObjectRefProperty>? prefabRefs)
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
            if (f.RszField.array) {
                var sourceType = f.RszField.original_type;
                if (!string.IsNullOrEmpty(sourceType)) {
                    // need to decipher field.RszField.original_type into a singular element type
                    // (can be empty, List`1<...>, ...[], maybe even Dictionary?)
                    // TODO should this be cached instead of re-computed each time?
                    if (sourceType.EndsWith("[]")){
                        f.ElementType = sourceType[..^2];
                    } else if (sourceType.StartsWith("System.Collections.Generic.List`1<")) {
                        f.ElementType = sourceType["System.Collections.Generic.List`1<".Length..^1];
                    } else {
                        // GD.Print("Unhandled array type " + sourceType);
                        // specifically via.motion.MotionFsm2Layer seems to get detected as original_type == element type
                        // may break other non-standard collections
                        f.ElementType = sourceType;
                    }
                }
            }
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
