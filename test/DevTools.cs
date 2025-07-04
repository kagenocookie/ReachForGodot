using Godot;
using ReeLib;
using ReeLib.Efx;
using ReeLib.Tools;

namespace ReaGE.Tests;

public static class DevTools
{
    public static readonly SupportedGame[] EfxSupportedGames = [
        SupportedGame.DevilMayCry5,
        SupportedGame.ResidentEvil4,
        SupportedGame.ResidentEvil7,
        SupportedGame.DragonsDogma2,
        SupportedGame.ResidentEvil2,
        SupportedGame.ResidentEvil3,
        SupportedGame.ResidentEvil8,
        SupportedGame.ResidentEvil2RT,
        SupportedGame.ResidentEvil3RT,
        SupportedGame.ResidentEvil7RT,
        // SupportedGame.MonsterHunterRise,
        // SupportedGame.StreetFighter6,
        // SupportedGame.MonsterHunterWilds,
    ];

    public static void WriteEfxStructsJson()
    {
        foreach (var efxVersion in Enum.GetValues<EfxVersion>()) {
            if (efxVersion == EfxVersion.Unknown) continue;

            var game = efxVersion.GetGameForEfxVersion();
            if (game == SupportedGame.Unknown) {
                GD.PrintErr("Unknown game for EFX version " + efxVersion);
                continue;
            }

            var outputPath = ProjectSettings.GlobalizePath($"res://userdata/output-efx/{game.ToShortName()}/efx_structs.json");

            if (efxVersion == EfxVersion.MHRise) {
                outputPath = outputPath.Replace(".json", "_base.json");
            }

            EfxTools.GenerateEFXStructsJson(outputPath, efxVersion);
            GD.Print($"Generated {efxVersion} EFX json to {outputPath}");
        }
    }

    public static IEnumerable<(EfxFile file, EFXAttribute matchedAttribute)> FindEfxAttributes(EfxAttributeType type, Func<EFXAttribute, bool> filter, params SupportedGame[] games)
    {
        return FindEfxAttributes<EFXAttribute>(type, filter, games);
    }

    public static IEnumerable<(EfxFile file, TAttr matchedAttribute)> FindEfxAttributes<TAttr>(EfxAttributeType type, Func<TAttr, bool> filter, params SupportedGame[] games) where TAttr : class
    {
        return FindEfxByAttribute((e) => e is TAttr ta && e.type == type && filter(ta), games)
            .SelectMany(efx => efx.Entries
                .SelectMany(e => e.Attributes
                    .Where(e => e is TAttr ta && e.type == type && filter(ta))
                    .Select(a => (efx, (a as TAttr)!))
                )
            );
    }

    public static IEnumerable<(EfxFile file, TAttr matchedAttribute)> FindEfxAttributes<TAttr>(Func<TAttr, bool> filter, params SupportedGame[] games) where TAttr : class
    {
        return FindEfxByAttribute((e) => e is TAttr ta && filter(ta), games)
            .SelectMany(efx => efx.Entries
                .SelectMany(e => e.Attributes
                    .Where(e => e is TAttr ta && filter(ta))
                    .Select(a => (efx, (a as TAttr)!))
                )
            );
    }

    public static List<EfxFile> FindEfxByAttribute(Func<EFXAttribute, bool> filter, params SupportedGame[] games)
    {
        return FindEfxWhere(f => f.Entries.Any(e => e.Attributes.Any(filter)), games);
    }

    public static List<EfxFile> FindEfxByAttributeType(EfxAttributeType type, params SupportedGame[] games)
    {
        if (games.Length == 0) {
            return FindEfxByAttributeType<EFXAttribute>(type);
        } else {
            return games.SelectMany(g => FindEfxByAttributeType<EFXAttribute>(type, g)).ToList();
        }
    }

    public static List<EfxFile> FindEfxByAttributeType<TAttr>(EfxAttributeType type, params SupportedGame[] games) where TAttr : EFXAttribute
    {
        return FindEfxWhere(f => f.Entries.Any(e => e.Attributes.Any(a => a is TAttr && a.type == type)), games);
    }

    public static List<EfxFile> FindEfxWhere(Func<EfxFile, bool> filter, params SupportedGame[] games)
    {
        if (games.Length == 0) games = ReachForGodot.ConfiguredGames.ToArray();

        return games.SelectMany(g => FindEfxWhere(filter, g)).ToList();
    }

    public static List<EfxFile> FindEfxWhere(Func<EfxFile, bool> filter, SupportedGame game)
    {
        return ReachForGodotPlugin.SelectFilesWhere(game, "efx", (g, filepath, stream) => {
            var file = new EfxFile(new FileHandler(stream, filepath));
            try {
                file.Read();
                if (filter.Invoke(file)) {
                    return file;
                }
            } catch (Exception) {
                // ignore
            }

            return null;
        }).Select(a => a.Item1).ToList();
    }
}
