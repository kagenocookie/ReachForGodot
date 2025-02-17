using System.Collections.Generic;
using System.IO;
using Godot;
using GC = Godot.Collections;

namespace RFG;

public static class ReachForGodot
{
    public static readonly SupportedGame[] GameList = Enum.GetValues<SupportedGame>().Where(n => n != SupportedGame.Unknown).ToArray();
    public static readonly string[] GameNames = Enum.GetNames<SupportedGame>().Where(n => n != SupportedGame.Unknown.ToString()).ToArray();

    private static readonly Dictionary<SupportedGame, (AssetConfig? config, GamePaths paths)> assetConfigData = new();

    public static string BlenderPath {
        get {
#if TOOLS
            return ReachForGodotPlugin.BlenderPath;
#else
            throw new NotImplementedException();
#endif
        }
    }

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

        var defaultResourcePath = "res://asset_config_" + game.ToString()!.ToSnakeCase();
        if (ResourceLoader.Exists(defaultResourcePath)) {
            data.config = ResourceLoader.Load<AssetConfig>(defaultResourcePath);
        } else {
            var da = DirAccess.Open("res://");
            string file;
            da.ListDirBegin();
            while ((file = da.GetNext()) != string.Empty) {
                if (ResourceLoader.Exists(file, nameof(AssetConfig))) {
                    var cfg = ResourceLoader.Load<Resource>(file) as AssetConfig;
                    if (cfg != null && cfg.Game == game) {
                        da.ListDirEnd();
                        data.config = cfg;
                        break;
                    }
                }
            }
        }

        if (data.config == null) {
            data.config = new AssetConfig() { ResourcePath = defaultResourcePath, AssetDirectory = data.paths.GetRszToolGameEnum().ToString() };
            ResourceSaver.Save(data.config);
        }

        if (data.paths != null) {
            assetConfigData[game] = data;
        }

        return data.config;
    }

    public static string? GetChunkPath(SupportedGame game) => GetPaths(game)?.ChunkPath;

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
