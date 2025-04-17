using System.Diagnostics.CodeAnalysis;
using Godot;

namespace ReaGE;

public static class ReachForGodot
{
    public static readonly SupportedGame[] GameList = Enum.GetValues<SupportedGame>().Where(n => n != SupportedGame.Unknown).ToArray();
    public static readonly string[] GameNames = Enum.GetNames<SupportedGame>().Where(n => n != SupportedGame.Unknown.ToString()).ToArray();

    private static readonly Dictionary<SupportedGame, AssetConfig> assetConfigs = new();
    private static readonly Dictionary<SupportedGame, GamePaths> gamePaths = new();
    public static IEnumerable<AssetConfig> AssetConfigs => GetAllAssetConfigs();
    public static IEnumerable<SupportedGame> ConfiguredGames => AssetConfigs.Where(c => c.IsValid).Select(c => c.Game);
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
        if (assetConfigs.Count == 0) ReloadSettings();
        return gamePaths.GetValueOrDefault(game);
    }

    public static AssetConfig GetAssetConfig(SupportedGame game)
    {
        if (assetConfigs.Count == 0) ReloadSettings();
        if (game == SupportedGame.Unknown) return AssetConfig.DefaultInstance;

        if (assetConfigs.TryGetValue(game, out var config)) {
            return config;
        }
        var paths = gamePaths.GetValueOrDefault(game);

        var gameShortname = paths?.ShortName ?? GamePaths.GetShortName(game);
        var defaultResourcePath = "res://asset_config_" + gameShortname + ".tres";
        if (ResourceLoader.Exists(defaultResourcePath)) {
            config = ResourceLoader.Load<AssetConfig>(defaultResourcePath);
            if (config != null) {
                return assetConfigs[game] = config;
            }
        } else {
            foreach (var cfg in FindAllAssetConfigs()) {
                if (cfg.Game == game) {
                    return assetConfigs[game] = cfg;
                }
            }
        }

        config = new AssetConfig() { ResourcePath = defaultResourcePath, AssetDirectory = gameShortname + "/", Game = game };
        ResourceSaver.Save(config);
        assetConfigs[game] = config;

        return config;
    }

    private static Dictionary<SupportedGame, AssetConfig>.ValueCollection GetAllAssetConfigs()
    {
        if (!didFullConfigScan) {
            didFullConfigScan = true;
            foreach (var newcfg in FindAllAssetConfigs()) {
                assetConfigs[newcfg.Game] = newcfg;
            }
        }
        return assetConfigs.Values;
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

    public static void SetPaths(SupportedGame game, GamePaths? paths)
    {
        if (paths == null) {
            if (gamePaths.Remove(game)) {
                GD.Print("Removing path config for " + game);
            }
        } else {
            gamePaths[game] = paths;
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