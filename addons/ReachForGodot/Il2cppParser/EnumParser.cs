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
}
