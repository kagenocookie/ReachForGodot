namespace REFDumpFormatter;

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

public partial class EnumParser
{
    public static Type? GetEnumBackingType(ObjectDef item)
    {
        if (item.fields == null) {
            return null;
        }

        foreach (var (fieldName, field) in item.fields) {
            if (fieldName == "value__") { // !IsStatic instead?
                return Type.GetType(field.Type);
            }
        }

        return typeof(int);
    }

    public static IEnumerable<(string name, object value)> GetSortedEnumValues(ObjectDef item, Type backingType)
    {
        if (item.fields == null) yield break;

        foreach (var (name, field) in item.fields.OrderBy(f => f.Value.Id)) {
            if (!field.Flags.Contains("SpecialName") && field.IsStatic && field.Default is JsonElement elem && elem.ValueKind == JsonValueKind.Number) {
                if (backingType == typeof(System.Int64)) {
                    yield return (name, elem.GetInt64());
                } else if (backingType == typeof(System.UInt64)) {
                    yield return (name, elem.GetUInt64());
                } else if (backingType == typeof(System.Int32)) {
                    var v = elem.GetInt64();
                    yield return (name, (int)(v >= 2147483648 ? (v - 2 * 2147483648L) : v));
                } else if (backingType == typeof(System.UInt32)) {
                    yield return (name, elem.GetUInt32());
                } else if (backingType == typeof(System.Int16)) {
                    var v = elem.GetInt32();
                    yield return (name, (short)(v >= 32768 ? (v - 2 * 32768) : v));
                } else if (backingType == typeof(System.UInt16)) {
                    yield return (name, elem.GetUInt16());
                } else if (backingType == typeof(System.SByte)) {
                    var v = elem.GetInt32();
                    yield return (name, (sbyte)(v >= 128 ? (v - 2 * 128) : v));
                } else if (backingType == typeof(System.Byte)) {
                    yield return (name, elem.GetByte());
                }
            }
        }
    }
}
