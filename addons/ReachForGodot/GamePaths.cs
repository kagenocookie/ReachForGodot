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
}
