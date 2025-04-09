using System.Diagnostics.CodeAnalysis;
using Godot;

namespace ReaGE;

public static class ReachForGodot
{
    public static readonly SupportedGame[] GameList = Enum.GetValues<SupportedGame>().Where(n => n != SupportedGame.Unknown).ToArray();
    public static readonly string[] GameNames = Enum.GetNames<SupportedGame>().Where(n => n != SupportedGame.Unknown.ToString()).ToArray();

    private static readonly Dictionary<SupportedGame, (AssetConfig? config, GamePaths? paths)> assetConfigData = new();
    public static IEnumerable<AssetConfig> AssetConfigs => GetAllAssetConfigs();
    public static IEnumerable<SupportedGame> ConfiguredGames => AssetConfigs.Select(c => c.Game);
    private static bool didFullConfigScan = false;

    public static string? BlenderPath {
        get {
#if TOOLS
            return ReachForGodotPlugin.BlenderPath;
#else
            throw new NotImplementedException();
#endif
        }
    }

    public static string GetUserdataPath(string path) => "res://userdata/" + path;

    public static bool IncludeMeshMaterial => ReachForGodotPlugin.IncludeMeshMaterial;
    public static int SceneFolderProxyThreshold => ReachForGodotPlugin.SceneFolderProxyThreshold;
    public static string? UnpackerExeFilepath => ReachForGodotPlugin.UnpackerExeFilepath;

    public static LabelledPathSetting? LastExportPath { get; set; }

    public static GamePaths? GetPaths(SupportedGame game)
    {
        if (assetConfigData.Count == 0) ReloadSettings();
        return assetConfigData.TryGetValue(game, out var data) ? data.paths : null;
    }

    public static AssetConfig GetAssetConfig(SupportedGame game)
    {
        if (assetConfigData.Count == 0) ReloadSettings();
        if (game == SupportedGame.Unknown) return AssetConfig.DefaultInstance;

        if (assetConfigData.TryGetValue(game, out var data)) {
            if (data.config != null) {
                return data.config;
            }
        }

        var gameShortname = data.paths?.ShortName ?? GamePaths.GetShortName(game);
        var defaultResourcePath = "res://asset_config_" + gameShortname + ".tres";
        if (ResourceLoader.Exists(defaultResourcePath)) {
            data.config = ResourceLoader.Load<AssetConfig>(defaultResourcePath);
        } else {
            foreach (var cfg in FindAllAssetConfigs()) {
                if (cfg.Game == game) {
                    data.config = cfg;
                    break;
                }
            }
        }

        if (data.config == null) {
            data.config = new AssetConfig() { ResourcePath = defaultResourcePath, AssetDirectory = gameShortname + "/", Game = game };
            ResourceSaver.Save(data.config);
        }

        if (data.paths != null) {
            assetConfigData[game] = data;
        }

        return data.config;
    }

    private static IEnumerable<AssetConfig> GetAllAssetConfigs()
    {
        if (!didFullConfigScan) {
            didFullConfigScan = true;
            foreach (var newcfg in FindAllAssetConfigs()) {
                if (!assetConfigData.TryGetValue(newcfg.Game, out var existing)) {
                    existing = (newcfg, null);
                } else {
                    existing = (newcfg, existing.paths);
                }
                assetConfigData[newcfg.Game] = existing;
            }
        }
        return assetConfigData.Values.Where(ac => ac.config != null).Select(ac => ac.config!);
    }

    private static IEnumerable<AssetConfig> FindAllAssetConfigs()
    {
        using var da = DirAccess.Open("res://");
        string file;
        da.ListDirBegin();
        while ((file = da.GetNext()) != string.Empty) {
            if (ResourceLoader.Exists(file, nameof(AssetConfig))) {
                var cfg = ResourceLoader.Load<Resource>(file) as AssetConfig;
                if (cfg != null && cfg.Game != SupportedGame.Unknown) {
                    yield return cfg;
                }
            }
        }
        da.ListDirEnd();
    }

#if TOOLS
    public static void ReloadSettings()
    {
        ReachForGodotPlugin.ReloadSettings();
    }

    public static void SetConfiguration(SupportedGame game, AssetConfig? config, GamePaths? paths)
    {
        if (paths == null) {
            assetConfigData.Remove(game);
        } else {
            assetConfigData[game] = (config, paths!);
        }
    }
#else
    public static void ReloadSettings()
    {
        throw new NotImplementedException();
    }
#endif
}

public record LabelledPathSetting(string path, string? label = null)
{
    public string DisplayLabel => label ?? path;

    [return: NotNullIfNotNull(nameof(setting))]
    public static implicit operator string?(LabelledPathSetting? setting) => setting?.path;
}