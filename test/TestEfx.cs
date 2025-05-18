using System.Globalization;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using ReaGE.EFX;
using RszTool;
using RszTool.Efx;
using RszTool.Efx.Structs;
using RszTool.Efx.Structs.DMC5;
using RszTool.Efx.Structs.RE4;
using RszTool.Efx.Structs.RERT;
using RszTool.Tools;
using Shouldly;

using ukn = RszTool.UndeterminedFieldType;

namespace ReaGE.Tests;

public partial class TestEfx : TestBase
{
    public TestEfx(Node testScene) : base(testScene) { }

    // [Test]
    // public async Task BasicImportExportTest()
    // {
    //     Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
    //     var converter = new AssetConverter(GodotImportOptions.testImport);
    //     var gameCounts = new Dictionary<SupportedGame, int>();
    //     await ExecuteFullReadTest("efx", async (game, fileOption, filepath) => {
    //         converter.Game = game;
    //         if (!gameCounts.TryGetValue(game, out var count)) {
    //             gameCounts[game] = count = 1;
    //         } else {
    //             gameCounts[game] = ++count;
    //         }
    //         if (count >= 10) return;

    //         using var file = new EfxFile(new FileHandler(filepath));
    //         try {
    //             file.Read();
    //         } catch (Exception e) {
    //             GD.PrintErr("Failed file " + Path.GetFileName(filepath) + ": " + e.Message + "/n" + filepath);
    //             return;
    //         }

    //         var node = new EfxRootNode() { Asset = new AssetReference(PathUtils.FullToRelativePath(filepath, converter.AssetConfig)!) };
    //         (await converter.Efx.Import(file, node)).ShouldBeTrue();
    //         converter.Efx.Clear();
    //         converter.Context.Clear();

    //         var exported = await ExportToMemory(converter.Efx, node, file.FileHandler.FileVersion);
    //         exported.ShouldNotBeNull();

    //         exported.Actions.Count.ShouldBe(file.Actions.Count);
    //         exported.Entries.Count.ShouldBe(file.Entries.Count);
    //         exported.Bones.Count.ShouldBe(file.Bones.Count);
    //         exported.ExpressionParameters.Count.ShouldBe(file.ExpressionParameters.Count);
    //         exported.FieldParameterValues.Count.ShouldBe(file.FieldParameterValues.Count);
    //         exported.CollisionEffects.Count.ShouldBe(file.CollisionEffects.Count);

    //         foreach (var (file_out, file_in) in PairEnumerate(exported.Entries, file.Entries)) {
    //             file_out.effectNameHash.ShouldBe(file_in.effectNameHash);
    //             file_out.name.ShouldBe(file_in.name);
    //             file_out.entryAssignment.ShouldBe(file_in.entryAssignment);
    //             file_out.index.ShouldBe(file_in.index);
    //             file_out.Attributes.Count.ShouldBe(file_in.Attributes.Count);
    //         }

    //         node.Free();
    //     }, null, EfxSupportedGames);
    // }

    [Test]
    public async Task FullReadWriteTest()
    {
        Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

        // var testfieldtypes = DevTools.FindEfxStructCandidate(exactLengthMatch: false, EfxVersion.DD2,
        //     typeof(uint),
        //     typeof(float),
        //     typeof(float),
        //     typeof(float),
        //     typeof(float), // color1
        //     typeof(float),
        //     typeof(float),
        //     typeof(float),
        //     typeof(float), //unkn7,
        //     typeof(float),
        //     typeof(float),
        //     typeof(float),
        //     typeof(float),
        //     typeof(float),
        //     typeof(float),
        //     typeof(float),
        //     typeof(float),
        //     typeof(float),
        //     typeof(float)
        // ).ToList();


        // var actionContainingEfx = DevTools.FindEfxWhere((efx, success) => efx.Actions.Count > 0);
        // var actionContainingEfx = DevTools.FindEfxWhere((efx, success) =>
        //     efx.Entries
        //         .Any(e =>
        //             e.Attributes.Any(a => a is EFXAttributeUnknownDD2_239) &&
        //             !e.Attributes.Any(a => a is EFXAttributeEffectOptimizeShader || a is EFXAttributeShaderSettings)
        //         )
        //     );

        // GD.Print(string.Join("\n", actionContainingEfx.Select(t => t.FileHandler.FilePath)));



        // var matches = DevTools.FindEfxAttributes<EFXAttributeUnknownDD2_239>(EfxAttributeType.UnknownDD2_239,
        //     // (attr) => attr.substructCount > 0 || attr.indicesCount > 0,
        //     (attr) => attr.v0.value2 != 0 || attr.v0.value3 != 0,
        //     // SupportedGame.ResidentEvil2RT)
        //     // SupportedGame.ResidentEvil2RT, SupportedGame.ResidentEvil3RT, SupportedGame.ResidentEvil7RT, SupportedGame.DevilMayCry5, SupportedGame.ResidentEvil4)
        //     // SupportedGame.ResidentEvil4)
        //     // SupportedGame.DevilMayCry5)
        //     SupportedGame.DragonsDogma2)
        // ;


        // // GD.Print(string.Join("\n", matches.Select(t => $"{t.matchedAttribute.Start} : {t.file.FileHandler.FilePath}")));
        // GD.Print(string.Join("\n", matches.Select(t => t.file.FileHandler.FilePath).Distinct()));

        // var solverAttrs = FindEfxAttributes(SupportedGame.ResidentEvil4, attr => attr.GetType().GetField("solverSize") != null);
        // // GD.Print(string.Join("\n", sampleFiles.Select(f => f.FileHandler.FilePath)));
        // var groups = new HashSet<(string src, string data)>();
        // foreach (var (file, attr) in solverAttrs) {
        //     var size = Convert.ToInt32(attr.GetType().GetField("solverSize")!.GetValue(attr));
        //     if (size > 0) {
        //         var data = (attr.GetType().GetField("data") ?? attr.GetType().GetField("expression"))?.GetValue(attr) as uint[];
        //         if (data != null) {
        //             var str = $"size: {size:000} args: {data[0]:000}, {data[1]:000}";
        //             groups.Add((attr.GetType().Name, str));
        //             GD.Print(str + "\t" + attr.GetType().Name + "\t" + file.FileHandler.FilePath);
        //         }
        //     } else {
        //         groups.Add((attr.GetType().Name, "size: 000"));
        //     }
        // }
        // GD.Print(string.Join("\n", groups.Order().Select(pair => (pair.data + "\t" + pair.src))));

        // using var file = new EfxFile(new FileHandler("E:/mods/dd2/REtool/re_chunk_000/natives/stm/gui/mastermaterial/ui03/ui030201/ui_dust_full_01.efx.4064419"));
        // using var file = new EfxFile(new FileHandler("E:/mods/re4/chunks/natives/stm/_chainsaw/vfx/effecteditor/efd_character/efd_ch_common/efd_0015_ch_common_damage_smoke_acid_0000.efx.3539837"));
        // using var file = new EfxFile(new FileHandler("E:/mods/re4/chunks/natives/stm/_chainsaw/vfx/effecteditor/efd_setmodel/efd_sm82_619_00/efd_0015_sm82_619_00_0000.efx.3539837"));
        // file.Read();
        // Debug.Break();

        // await FullReadTest();
        // await DumpEfxAttributeUsageList();
        // await DumpEfxStructValueLists<EFXAttributeFluidSimulator2D>();
        await DumpEfxStructValueLists();

        var fieldInconsistencies = await FindInconsistentEfXFields();

        if (fieldInconsistencies.Count > 0) {
            GD.Print("Found field type inconsistencies:");
        }
        HashSet<Type> ignoreTypes = [typeof(EFXAttributeFixRandomGenerator), typeof(EFXAttributeUnitCulling)];
        HashSet<string> ignoreFieldNames = ["mdfPropertyHash"];
        foreach (var (attrType, inco) in fieldInconsistencies.OrderBy(fi => fi.Key.FullName)) {
            if (ignoreTypes.Contains(attrType)) continue;
            foreach (var (field, data) in inco.OrderBy(o => o.Key)) {
                if (ignoreFieldNames.Contains(field)) continue;
                GD.Print($"{attrType.Name}: {field}\n{string.Join(", ", data.values)}");
                // GD.Print($"{attrType.Name}: {field}\n{string.Join("\n", data.filepaths)}\n{string.Join(", ", data.values)}");
            }
            GD.Print("");
            // await DumpEfxStructValueLists(attrType);
        }
    }

    private static async Task FullReadTest()
    {
        var converter = new AssetConverter(GodotImportOptions.testImport);
        await ExecuteFullReadTest("efx", async (game, fileOption, filepath) => {
            using var file = new EfxFile(new FileHandler(filepath));
            try {
                file.Read();
                file.FileHandler.Position.ShouldBe(file.FileHandler.Stream.Length, "File was not fully read");
            } catch (Exception e) {
                GD.PrintErr("Failed file " + Path.GetFileName(filepath) + ": " + e.Message + "/n" + filepath);
                return;
            }

            // converter.Game = game;
            // using var file = converter.Efx.CreateFile(filepath);
            // converter.Uvar.LoadFile(file);

        }, null, DevTools.EfxSupportedGames);
        // }, null, SupportedGame.ResidentEvil3);
        // }, null, SupportedGame.ResidentEvil8);
        // }, null, SupportedGame.DragonsDogma2);
        // }, null, SupportedGame.ResidentEvil7);
        // }, null, SupportedGame.ResidentEvil4);
    }

    private static async Task<Dictionary<Type, Dictionary<string, (HashSet<string> values, HashSet<string> filepaths)>>> FindInconsistentEfXFields()
    {

        var fieldInconsistencies = new Dictionary<Type, Dictionary<string, (HashSet<string> values, HashSet<string> filepaths)>>();
        void AddInconsistency(Type type, string field, string valueInfo, string filepath)
        {
            if (!fieldInconsistencies.TryGetValue(type, out var dict)) {
                fieldInconsistencies[type] = dict = new();
            }

            if (!dict.TryGetValue(field, out var data)) {
                dict[field] = data = new() { filepaths = new(), values = new() };
            }

            data.values.Add(valueInfo);
            data.filepaths.Add(filepath);
        }

        var converter = new AssetConverter(GodotImportOptions.testImport);
        await ExecuteFullReadTest("efx", async (game, fileOption, filepath) => {
            using var file = new EfxFile(new FileHandler(filepath));
            try {
                file.Read();
                file.FileHandler.Position.ShouldBe(file.FileHandler.Stream.Length, "File was not fully read");
            } catch (Exception e) {
                GD.PrintErr("Failed file " + Path.GetFileName(filepath) + ": " + e.Message + "/n" + filepath);
                return;
            }

            static bool LooksLikeFloat(int n) => BitConverter.Int32BitsToSingle(n) is float f && Mathf.Abs(f) > 0.00001f && Mathf.Abs(f) < 10000f;

            foreach (var a in file.GetAttributesAndActions(true)) {
                var attrType = a.GetType();
                var fields = attrType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var f in fields) {
                    if (f.FieldType == typeof(ukn) && f.GetValue(a) is ukn uu && uu.value != 0) {
                        AddInconsistency(attrType, f.Name, uu.ToString(), filepath);
                    } else if (f.FieldType == typeof(int) && f.GetValue(a) is int ii && LooksLikeFloat(ii)) {
                        AddInconsistency(attrType, f.Name, BitConverter.Int32BitsToSingle(ii).ToString("0.0#"), filepath);
                    } else if (f.FieldType == typeof(uint) && f.GetValue(a) is uint n && LooksLikeFloat((int)n)) {
                        AddInconsistency(attrType, f.Name, BitConverter.Int32BitsToSingle((int)n).ToString("0.0#"), filepath);
                    } else if (f.FieldType == typeof(float) && f.GetValue(a) is float flt &&
                        (Math.Abs(flt) > 100000000 || flt != 0 && BitConverter.SingleToUInt32Bits(flt) < 1000)
                    ) {
                        AddInconsistency(attrType, f.Name, new ukn(flt).ToString(), filepath);
                    }
                }
            }

            // converter.Game = game;
            // using var file = converter.Efx.CreateFile(filepath);
            // converter.Uvar.LoadFile(file);

            // }, null, SupportedGame.ResidentEvil3);
        }, null, DevTools.EfxSupportedGames);
        // }, null, SupportedGame.ResidentEvil8);
        // }, null, SupportedGame.DragonsDogma2);
        // }, null, SupportedGame.ResidentEvil7);

        return fieldInconsistencies;
    }

    private static async Task DumpEfxAttributeUsageList()
    {
        var attributeTypeUsages = new Dictionary<EfxVersion, Dictionary<EfxAttributeType, HashSet<string>>>();
        await ExecuteFullReadTest("efx", async (game, fileOption, filepath) => {
            using var file = new EfxFile(new FileHandler(filepath));
            try {
                file.Read();
            } catch (Exception e) {
                GD.PrintErr("Failed file " + Path.GetFileName(filepath) + ": " + e.Message + "/n" + filepath);
                return;
            }

            foreach (var attr in file.GetAttributesAndActions(true)) {
                if (!attributeTypeUsages.TryGetValue(file.Header!.Version, out var verUsages)) {
                    attributeTypeUsages[file.Header!.Version] = verUsages = new();
                }
                if (!verUsages.TryGetValue(attr.type, out var usages)) {
                    verUsages[attr.type] = usages = new();
                }
                usages.Add(filepath);
            }
        }, null, DevTools.EfxSupportedGames);

        var usageSb = new StringBuilder();
        foreach (var (version, attrPaths) in attributeTypeUsages.OrderBy(atu => atu.Key)) {
            usageSb
                .AppendLine("----------------------------------")
                .Append("Game: ").AppendLine(version.ToString())
                .AppendLine();
            // EfxAttributeTypeRemapper.ToAttributeTypeID(version)

            foreach (var (attr, paths) in attrPaths.OrderBy(k => EfxAttributeTypeRemapper.ToAttributeTypeID(version, k.Key))) {
                usageSb.Append(attr.ToString()).AppendLine($" ({paths.Count})");
            }
            usageSb.AppendLine("----------------------------------");

            foreach (var (attr, paths) in attrPaths.OrderBy(k => EfxAttributeTypeRemapper.ToAttributeTypeID(version, k.Key))) {
                usageSb.AppendLine().Append(attr.ToString()).AppendLine($" ({paths.Count})");
                foreach (var path in paths.OrderBy(x => x)) {
                    usageSb.AppendLine(path);
                }
            }
            usageSb.AppendLine();
        }
        File.WriteAllText(ProjectSettings.GlobalizePath(ReachForGodot.GetUserdataBasePath("efx_field_values/__usages.txt")), usageSb.ToString());
    }


    private static Task DumpEfxStructValueLists<T>() where T : EFXAttribute
        => DumpEfxStructValueLists(typeof(T));

    private static async Task DumpEfxStructValueLists(Type? targetType = null)
    {
        var dict = new Dictionary<Type, Dictionary<string, Dictionary<EfxVersion, HashSet<string>>>>();
        await ExecuteFullReadTest("efx", async (game, fileOption, filepath) => {
            using var file = new EfxFile(new FileHandler(filepath));
            try {
                file.Read();
            } catch (Exception e) {
                GD.PrintErr("Failed file " + Path.GetFileName(filepath) + ": " + e.Message + "/n" + filepath);
                return;
            }

            foreach (var attr in file.GetAttributesAndActions(true)) {
                var attrType = attr.GetType();
                if (targetType != null && attrType != targetType) continue;

                if (!dict.TryGetValue(attrType, out var allValues)) {
                    dict[attrType] = allValues = new();
                }
                if (attr.type == EfxAttributeType.TypePolygonClip) {
                    Debug.Break();
                }

                var fields = EfxTools.GetFieldInfo(attrType, file.Header!.Version);
                foreach (var (fname, ftype) in fields) {
                    if (!allValues.TryGetValue(fname, out var values)) {
                        allValues[fname] = values = new();
                    }
                    if (!values.TryGetValue(file.Header!.Version, out var fieldValues)) {
                        values[file.Header!.Version] = fieldValues = new();
                    }
                    var fi = attrType.GetField(fname, BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                    if (fi == null) continue;
                    var value = fi.GetValue(attr);
                    if (value is int i) fieldValues.Add(new ukn(i).GetMostLikelyValueTypeString());
                    else if (value is uint u) fieldValues.Add(new ukn(u).GetMostLikelyValueTypeString());
                    else if (value is float f) fieldValues.Add(new ukn(f).GetMostLikelyValueTypeString());
                    else if (value is RszTool.via.Color c) fieldValues.Add(new ukn(c.rgba).GetMostLikelyValueTypeString());
                    else if (value is ukn uu) fieldValues.Add(uu.GetMostLikelyValueTypeString());
                    else fieldValues.Add(value?.ToString() ?? "NULL");
                }
            }
        }, null, DevTools.EfxSupportedGames);

        Directory.CreateDirectory(ProjectSettings.GlobalizePath(ReachForGodot.GetUserdataBasePath("efx_field_values")));
        var usageSb = new StringBuilder();
        foreach (var (attrType, allValues) in dict) {
            usageSb.Clear();
            foreach (var (field, versions) in allValues.OrderBy(atu => attrType.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Select(f => f.Name).ToList().IndexOf(atu.Key))) {
                usageSb
                    .AppendLine("----------------------------------")
                    .Append("Field: ").AppendLine(field.ToString())
                    .AppendLine();

                foreach (var (version, values) in versions.OrderBy(k => k.Key)) {
                    usageSb.Append(version.ToString()).Append(": ").AppendLine(string.Join(", ", values));
                }
                usageSb.AppendLine();
            }
            File.WriteAllText(ProjectSettings.GlobalizePath(ReachForGodot.GetUserdataBasePath(
                $"efx_field_values/{attrType.Name.Replace("EFXAttribute", "")}__{attrType.Namespace}.txt"
            )), usageSb.ToString());
        }
    }
}
