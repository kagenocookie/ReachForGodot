using System.Text.Json.Serialization;

namespace REFDumpFormatter;

public class FieldDef
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    // [JsonPropertyName("offset_from_base")]
    // public string OffsetFromBase { get; set; } = string.Empty;

    // public int OffsetNumber => Convert.ToInt32(OffsetFromBase, 16);

    // [JsonPropertyName("offset_from_fieldptr")]
    // public string OffsetFromFieldPointer { get; set; } = string.Empty;

    [JsonPropertyName("flags")]
    public string Flags { get; set; } = string.Empty;

    [JsonPropertyName("default")]
    public object? Default { get; set; }

    public bool IsStatic => Flags.Contains("Static");
    public bool IsPrivate => Flags.Contains("Private");

    // public string Modifiers {
    //     get {
    //         var modifiers = string.Empty;
    //         if (IsStatic) {
    //             modifiers += "static ";
    //         }
    //         return modifiers;
    //     }
    // }
}