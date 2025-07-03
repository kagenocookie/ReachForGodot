using Godot;
using ReeLib;

namespace ReaGE;

public record GamePaths(
    SupportedGame Game,
    string ChunkPath,
    string? Gamedir,
    LabelledPathSetting[] AdditionalPaths,
    string[] PakFiles
) {
    public GamePaths(SupportedGame game) : this(game, string.Empty, null, Array.Empty<LabelledPathSetting>(), Array.Empty<string>()) { }

    public string? SourcePathOverride { get; set; }
    public GameMasterConfig? MasterConfig { get; set; }

    public static readonly string RszPatchGlobalPath = ProjectSettings.GlobalizePath($"res://addons/ReachForGodot/game_config/global/rsz_patches.json");

    public string IgnoredFilesListPath => ProjectSettings.GlobalizePath(GetGameConfigPath(Game, "ignored_files.list"));
    public string MasterConfigPath => GetMasterConfigFilepath(Game);

    public static string GetMasterConfigFilepath(SupportedGame game) => ProjectSettings.GlobalizePath(GetGameConfigPath(game, "config.json"));

    public static string GetGameConfigPath(SupportedGame game, string subpath)
        => $"res://addons/ReachForGodot/game_config/{game.ToShortName()}/{subpath}";

    public GameName GetReeLibGameEnum()
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
            case SupportedGame.MonsterHunterWilds: return GameName.mhwilds;
            default: return GameName.unknown;
        }
    }

    public string ShortName => Game.ToShortName();
}
