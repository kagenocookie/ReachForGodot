namespace ReaGE.ContentEditorIntegration;

using System.Text.Json;
using System.Text.Json.Serialization;

public class ContentEditorEnumDefinition
{
    [JsonPropertyName("displayLabels")]
    public Dictionary<string, string>? DisplayLabels { get; set; }

    [JsonPropertyName("values")]
    public Dictionary<string, JsonElement>? Values { get; set; }
}
