namespace ReaGE;

using Godot;
using Godot.Collections;

[GlobalClass, Tool]
public partial class MaterialPropertyList : Resource
{
    [Export] public int[]? ValueCounts { get; set; }
    [Export] public string[]? Names { get; set; }
    [Export] public Godot.Collections.Array? Values { get; set; }

    private Array<Dictionary>? propertyList;

    public MaterialPropertyList()
    {
    }

    public MaterialPropertyList(int propertyCount)
    {
        ValueCounts = new int[propertyCount];
        Names = new string[propertyCount];
    }

    public void SetParam(int index, Variant value, string name)
    {
        if (Names == null || index >= Names.Length) return;
        ValueCounts![index] = value.VariantType == Variant.Type.Int ? 1 : 4;
        Names![index] = name;
        Values ??= new();
        while (Values.Count <= index) {
            Values.Add(0);
        }
        Values[index] = value;
    }

    public override void _ValidateProperty(Dictionary property)
    {
        if (property["name"].AsStringName() == PropertyName.ValueCounts) {
            property["usage"] = (int)(PropertyUsageFlags.Storage);
        }
        if (property["name"].AsStringName() == PropertyName.Values) {
            property["usage"] = (int)(PropertyUsageFlags.Storage);
        }
        if (property["name"].AsStringName() == PropertyName.Names) {
            property["usage"] = (int)(PropertyUsageFlags.Storage);
        }
        base._ValidateProperty(property);
    }

    public override Variant _Get(StringName property)
    {
        if (Names == null || Values == null) return default;

        var index = System.Array.IndexOf(Names, property.ToString());
        if (index != -1) {
            return Values[index];
        }
        return base._Get(property);
    }

    public override bool _Set(StringName property, Variant value)
    {
        if (Names == null || Values == null) return default;

        var index = System.Array.IndexOf(Names, property.ToString());
        if (index != -1) {
            Values[index] = value;
            return true;
        }
        return false;
    }

    public override Array<Dictionary> _GetPropertyList()
    {
        if (propertyList == null) {
            propertyList = new();
            ValueCounts ??= new int[Names?.Length ?? Values?.Count ?? 0];
            for (int i = 0; i < Names?.Length; ++i) {
                var count = i < ValueCounts.Length ? ValueCounts[i] : 4;
                var type = count switch {
                    1 => Variant.Type.Float,
                    4 => Variant.Type.Color,
                    _ => Variant.Type.PackedByteArray,
                };

                propertyList.Add(new Dictionary() {
                    { "name", Names[i] },
                    { "type", (int)type },
                    { "usage", (int)(PropertyUsageFlags.Editor|PropertyUsageFlags.ScriptVariable) }
                });
            }
        }

        return propertyList;
    }
}
