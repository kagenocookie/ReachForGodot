namespace ReaGE;

using ReeLib;

public enum SupportedGame
{
    Unknown = 0,
    DragonsDogma2 = 1,
    DevilMayCry5 = 2,
    ResidentEvil2 = 3,
    ResidentEvil2RT = 4,
    ResidentEvil3 = 5,
    ResidentEvil3RT = 6,
    ResidentEvil4 = 7,
    ResidentEvil7 = 8,
    ResidentEvil7RT = 9,
    ResidentEvil8 = 10,
    MonsterHunterRise = 11,
    StreetFighter6 = 12,
    MonsterHunterWilds = 13,
}

public static class EnumExtensions
{
    public static bool UsesEmbeddedUserdata(this SupportedGame game) => game switch {
        SupportedGame.DevilMayCry5 => true,
        SupportedGame.ResidentEvil2 => true,
        SupportedGame.ResidentEvil7 => true,
        _ => false
    };

    public static string ToShortName(this SupportedGame game) => game switch {
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
        _ => game.ToString(),
    };

    public static SupportedGame FromReeLibEnum(this GameName game) => game switch {
        GameName.dd2 => SupportedGame.DragonsDogma2,
        GameName.dmc5 => SupportedGame.DevilMayCry5,
        GameName.re2 => SupportedGame.ResidentEvil2,
        GameName.re2rt => SupportedGame.ResidentEvil2RT,
        GameName.re3 => SupportedGame.ResidentEvil3,
        GameName.re3rt => SupportedGame.ResidentEvil3RT,
        GameName.re4 => SupportedGame.ResidentEvil4,
        GameName.re7 => SupportedGame.ResidentEvil7,
        GameName.re7rt => SupportedGame.ResidentEvil7RT,
        GameName.re8 => SupportedGame.ResidentEvil8,
        GameName.mhrise => SupportedGame.MonsterHunterRise,
        GameName.sf6 => SupportedGame.StreetFighter6,
        GameName.mhwilds => SupportedGame.MonsterHunterWilds,
        _ => SupportedGame.Unknown,
    };
}

public enum ImportMode
{
    AlwaysExtractFiles,
    PreferSourcePAK,
}
