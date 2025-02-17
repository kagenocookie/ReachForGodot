using System.Text.Json.Serialization;

namespace REFDumpFormatter;

public class ParamDef
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("modifier")]
    public string? Modifier { get; set; }

    public bool ByRef => Modifier == "ByRef";
}
