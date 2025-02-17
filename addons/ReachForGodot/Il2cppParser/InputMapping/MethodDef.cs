using System.Text.Json.Serialization;

namespace REFDumpFormatter;

public class MethodDef
{
    public string? function { get; set; }
    public int id { get; set; }

    [JsonPropertyName("params")]
    public ParamDef?[]? Params { get; set; }

    [JsonPropertyName("returns")]
    public ParamDef? Returns { get; set; }

    [JsonPropertyName("flags")]
    public string? Flags { get; set; }

    public bool IsStatic => Flags?.Contains("Static") == true;
    public bool IsVirtual => Flags?.Contains("Virtual") == true;
    public bool IsAbstract => Flags?.Contains("Abstract") == true;
    public bool IsFinal => Flags?.Contains("Final") == true;
    public bool IsPrivate => Flags?.Contains("Private") == true;

    public string Modifiers {
        get {
            var modifiers = string.Empty;
            if (IsStatic) {
                modifiers += "static ";
            } else if (IsVirtual) {
                modifiers += "virtual ";
            }
            return modifiers;
        }
    }
}
