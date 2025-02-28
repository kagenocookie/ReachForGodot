using Godot;
using RszTool;

namespace RGE;

public record GamePaths(SupportedGame Game, string ChunkPath, string? Il2cppPath, string? RszJsonPath)
{
    public GameName GetRszToolGameEnum()
    {
        switch (Game) {
            case SupportedGame.DragonsDogma2: return GameName.dd2;
            case SupportedGame.DevilMayCry5: return GameName.dmc5;
            case SupportedGame.ResidentEvil2: return GameName.re2;
            case SupportedGame.ResidentEvil2RT: return GameName.re2rt;
            case SupportedGame.ResidentEvil3: return GameName.re3;
            case SupportedGame.ResidentEvil3RT: return GameName.re3rt;
            case SupportedGame.ResidentEvil4: return GameName.re4;
            case SupportedGame.ResidentEvil7: return GameName.re7;
            case SupportedGame.ResidentEvil7RT: return GameName.re7rt;
            case SupportedGame.ResidentEvil8: return GameName.re8;
            case SupportedGame.MonsterHunterRise: return GameName.mhrise;
            case SupportedGame.StreetFighter6: return GameName.sf6;
            case SupportedGame.MonsterHunterWilds: return GameName.unknown;
            default: return GameName.unknown;
        }
    }

    public string ShortName => Game switch {
        SupportedGame.DragonsDogma2 => "dd2",
        SupportedGame.DevilMayCry5 => "dmc5",
        SupportedGame.ResidentEvil2 => "re2",
        SupportedGame.ResidentEvil2RT => "re2rt",
        SupportedGame.ResidentEvil3 => "re3",
        SupportedGame.ResidentEvil3RT => "re3rt",
        SupportedGame.ResidentEvil4 => "re4",
        SupportedGame.ResidentEvil7 => "re7",
        SupportedGame.ResidentEvil7RT => "re7rt",
        SupportedGame.ResidentEvil8 => "re8",
        SupportedGame.MonsterHunterRise => "mhrise",
        SupportedGame.StreetFighter6 => "sf6",
        SupportedGame.MonsterHunterWilds => "mhwilds",
        _ => Game.ToString(),
    };

    public string GetChunkRelativePath(string path) => string.IsNullOrEmpty(ChunkPath) ? path : path.Replace(ChunkPath, "");

    public static readonly string RszPatchGlobalPath = ProjectSettings.GlobalizePath($"res://addons/ReachForGodot/game_config/global/rsz_patches.json");
    public string RszPatchPath => ProjectSettings.GlobalizePath($"res://addons/ReachForGodot/game_config/{ShortName}/rsz_patches.json");

    public string EnumCacheFilename => ProjectSettings.GlobalizePath($"res://addons/ReachForGodot/game_config/{ShortName}/il2cpp_cache.json");
    public string EnumOverridesFilename => ProjectSettings.GlobalizePath($"res://addons/ReachForGodot/game_config/{ShortName}/il2cpp_cache.en.json");
    public string PfbGameObjectRefPropsPath => ProjectSettings.GlobalizePath($"res://addons/ReachForGodot/game_config/{ShortName}/pfb_ref_props.json");
    public string ExtensionVersionsCacheFilepath => ProjectSettings.GlobalizePath($"res://addons/ReachForGodot/game_config/{ShortName}/file_extensions.json");
}
