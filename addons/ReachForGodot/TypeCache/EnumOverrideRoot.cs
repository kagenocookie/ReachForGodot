namespace ReaGE;

using System.Text.Json;

public class EnumOverrideRoot
{
    public List<EnumCacheItem>? DisplayLabels { get; set; }
}

public class EnumOverrideDisplayLabelEntry
{
    public JsonElement Value { get; set; }
    public string? Label { get; set; }
}
