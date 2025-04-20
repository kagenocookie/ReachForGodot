namespace ReaGE.ContentEditorIntegration;

using System.Text.Json;
using Godot;

public class ContentEditor
{
    public AssetConfig Config { get; }
    public string Gamepath { get; }

    public static readonly JsonSerializerOptions jsonOptions = new() {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault,
    };

    public ContentEditor(AssetConfig config, string gamepath)
    {
        Config = config;
        Gamepath = gamepath;
    }

    public bool ParseDumpedEnumDisplayLabels()
    {
        var enumDumpDir = $"{Gamepath}/reframework/data/usercontent/dumps/";
        if (!Directory.Exists(enumDumpDir)) return false;

        var latestEnumDumpDir = Directory.EnumerateDirectories(enumDumpDir).LastOrDefault();
        if (latestEnumDumpDir == null) return false;

        var enumFiles = Directory.EnumerateFiles(latestEnumDumpDir, "*.json");
        var overrideDir = Config.Paths.EnumOverridesDir;
        foreach (var filepath in enumFiles) {
            var classname = Path.GetFileNameWithoutExtension(filepath);
            var overridePath = Path.Combine(overrideDir, classname + ".json");
            EnumOverrideRoot? currentOverrides = null;

            var desc = TypeCache.GetEnumDescriptor(Config.Game, classname);
            using var fs = File.OpenRead(filepath);
            var ceEnum = JsonSerializer.Deserialize<ContentEditorEnumDefinition>(fs, jsonOptions);
            var changed = false;
            if (ceEnum?.DisplayLabels != null && ceEnum.Values != null) {
                foreach (var (name, label) in ceEnum.DisplayLabels) {
                    var safeLabel = label.Replace(":", "-");
                    if (ceEnum.Values.TryGetValue(name, out var value) && desc.GetLabel(value) != safeLabel) {
                        desc.AddValue(safeLabel, value);
                        currentOverrides ??= DeserializeOrDefault<EnumOverrideRoot>(overridePath) ?? new();
                        changed = true;
                        currentOverrides.DisplayLabels ??= new();
                        currentOverrides.DisplayLabels.Add(new EnumCacheItem(safeLabel, value));
                    }
                }
            }

            if (changed && currentOverrides != null) {
                Directory.CreateDirectory(overridePath.GetBaseDir());
                File.WriteAllText(overridePath, JsonSerializer.Serialize(currentOverrides, jsonOptions));
                GD.Print($"Updated enum: {filepath}");
            }
        }
        return true;
    }

    private T? DeserializeOrDefault<T>(string filepath)
    {
        if (!File.Exists(filepath)) return default;
        using var fs = File.OpenRead(filepath);
        return JsonSerializer.Deserialize<T>(fs, jsonOptions);
    }
}