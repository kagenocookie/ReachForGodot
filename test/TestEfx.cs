using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using RszTool;
using RszTool.Efx;
using RszTool.Efx.Structs.Common;
using RszTool.Efx.Structs.RE4;
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

        // var matches = DevTools.FindEfxAttributes<EFXAttributeTransform3DClip>(EfxAttributeType.Transform3DClip,
        //     // (attr) => attr.substructCount > 0 || attr.indicesCount > 0,
        //     // (attr) => attr.expressions?.solverSize > 0
        //     // (attr) => attr.expression3 != null
        //     (attr) => attr.clipData.substruct3Count > 0
        //     // SupportedGame.ResidentEvil2RT
        //     // SupportedGame.ResidentEvil2RT, SupportedGame.ResidentEvil3RT, SupportedGame.ResidentEvil7RT, SupportedGame.DevilMayCry5, SupportedGame.ResidentEvil4
        //     // SupportedGame.ResidentEvil4
        //     // SupportedGame.DevilMayCry5
        //     // SupportedGame.ResidentEvil4
        //     )
        // ;
        // GD.Print(string.Join("\n", matches.Select(t => t.file.FileHandler.FilePath).Distinct()));

        // var matches = DevTools.FindEfxByAttribute(attr => attr is IExpressionAttribute exprAttr && exprAttr.Expression != null &&
        //     BitOperations.PopCount(Convert.ToUInt32(attr.GetType().GetField(EfxTools.GetFieldInfo(attr.GetType(), attr.Version).First().name)!.GetValue(attr))) != exprAttr.Expression.ExpressionCount);
        // var matches = DevTools.FindEfxByAttribute<ValueTaskParentOptionsExpression(attr => attr is IExpressionAttribute exprAttr && exprAttr.Expression != null && exprAttr.Expression.Expressions.Any(exp => exp.Components.Count() > 40));
        // var matches = DevTools.FindEfxByAttribute(attr => attr is IExpressionAttribute exprAttr && exprAttr.Expression != null && exprAttr.Expression.Expressions.Any(exp => exp.Components.Count() > 40));
        // var matches = DevTools.FindEfxByAttribute(attr => attr is IClipAttribute clipAttr && true == clipAttr.Clip.frames?.Any(f => f.value.GetMostLikelyValueTypeObject() is int));
        // var matches = DevTools.FindEfxAttributes<IClipAttribute>(attr => attr is IClipAttribute clipAttr && clipAttr.GetType().Name.Contains("ColorClip") && true == clipAttr.Clip.frames?.Any(f => f.value.GetMostLikelyValueTypeObject() is float));
        // var matches = DevTools.FindEfxAttributes<EFXAttributeTextureUnitExpression>(a => true);
        // // var matches = DevTools.FindEfxAttributes<IMaterialClipAttribute>(
        // // var matches = DevTools.FindEfxAttributes<EFXAttributeTypeStrainRibbonMaterialClip>(
        // // //     attr => attr.clipData.frames != null && attr.clipData.clips != null
        // // //     && attr.clipData.clips.Length != BitOperations.PopCount(attr.colorChannelBits)
        // // //     // && attr.unkn0
        // // // );
        // //     attr => attr.MaterialClip.frames != null && attr.MaterialClip.clips != null
        // //     // && attr.clipBits < 0xf00
        // //     // && attr.MaterialClip.mdfPropertyCount > 0
        // //     // && attr.MaterialClip.mdfProperties.Length != (attr.MaterialClip.mdfPropertyCount == 0 ? attr.MaterialClip.clipCount : attr.MaterialClip.mdfPropertyCount)
        // //     // && attr.unkn0
        // // );
        // // var matches = DevTools.FindEfxAttributes<IClipAttribute>(attr => attr is IClipAttribute clipAttr && clipAttr.Clip.frames != null && clipAttr.Clip.clips != null
        // //     // && clipAttr.Clip.frames.Any(f => f.value.GetMostLikelyValueTypeObject() is int)
        // //     && clipAttr.Clip.clips.Any(f => f.unkn1 != 3)
        // // );

        // // GD.Print(string.Join("\n", matches.Select(t => t.FileHandler.FilePath).Distinct()));
        // GD.Print(string.Join("\n", matches.Select(t => t.file.FileHandler.FilePath + ": " + t.matchedAttribute).Distinct()));

        // using var file = new EfxFile(new FileHandler("E:/mods/dd2/REtool/re_chunk_000/natives/stm/gui/mastermaterial/ui03/ui030201/ui_dust_full_01.efx.4064419"));
        // using var file = new EfxFile(new FileHandler("E:/mods/re4/chunks/natives/stm/_chainsaw/vfx/effecteditor/efd_character/efd_ch_common/efd_0015_ch_common_damage_smoke_acid_0000.efx.3539837"));
        // using var file = new EfxFile(new FileHandler("J:/mods/re4/chunks/natives/stm/_anotherorder/vfx/effecteditor/efd_character/efd_ao_chc3/efd_0015_ao_chc3_hold_dead_0000.efx.3539837"));
        // file.Read();
        // Debug.Break();

        // await FullExpressionParseTest();
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
                file.ParseExpressions();
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

    private static async Task FullExpressionParseTest()
    {
        var converter = new AssetConverter(GodotImportOptions.testImport);
        await ExecuteFullReadTest("efx", async (game, fileOption, filepath) => {
            using var file = new EfxFile(new FileHandler(filepath));
            try {
                file.Read();
                file.FileHandler.Position.ShouldBe(file.FileHandler.Stream.Length, "File was not fully read");
                file.ParseExpressions();
                foreach (var a in file.GetAttributesAndActions(true)) {
                    if (a is IExpressionAttribute expr1 && expr1.Expression?.ParsedExpressions != null) {
                        VerifyExpressionCorrectness(game, filepath, file, expr1.Expression);
                    }
                    if (a is IMaterialExpressionAttribute expr2 && expr2.MaterialExpressions?.ParsedExpressions != null) {
                        VerifyExpressionCorrectness(game, filepath, file, expr2.MaterialExpressions);
                    }
                }
            } catch (Exception e) {
                GD.PrintErr("Failed file " + Path.GetFileName(filepath) + ": " + e.Message + "/n" + filepath);
                return;
            }

            void VerifyExpressionCorrectness(SupportedGame game, string filepath, EfxFile file, EFXExpressionContainer a)
            {
                var parsedlist = (a as EFXExpressionList)?.ParsedExpressions ?? (a as EFXMaterialExpressionList)!.ParsedExpressions!;
                var srclist = (a as EFXExpressionList)?.Expressions ?? (a as EFXMaterialExpressionList)!.Expressions!;
                for (var i = 0; i < parsedlist.Count; i++) {
                    var srcExp = srclist.ElementAt(i);
                    var parsed = parsedlist[i];
                    var originalStr = parsed.ToString();

                    // if (srcExp.components.Count > 1) Console.WriteLine(originalStr);
                    var tree = EfxExpressionParser.Parse(originalStr, parsed.parameters);
                    var reParsedStr = tree.ToString();
                    // TODO: properly handle material expression additional fields
                    reParsedStr.ShouldBe(originalStr);
                    var reFlattened = file.FlattenExpressionTree(tree);
                    reFlattened.parameters?.OrderBy(p => p.parameterNameHash).ShouldBeEquivalentTo(srcExp.parameters?.OrderBy(p => p.parameterNameHash));
                    // we can't do full 100% per-component comparison because we lost parentheses during the tostring conversion
                    // in other words, `a + (b + c)` would get re-serialized as `(a + b) + c`, which isn't a meaningful difference content wise but would serialize differently
                    if (game == SupportedGame.DevilMayCry5 && (
                        Path.GetFileName(filepath.AsSpan()).SequenceEqual("efd_03_l03_018_00.efx.1769672") ||
                        Path.GetFileName(filepath.AsSpan()).SequenceEqual("efd_03_l03_011_00.efx.1769672")
                    )) {
                        // let these be incomplete because they're the only files that have this issue
                    } else {
                        reFlattened.components.Count.ShouldBe(srcExp.components.Count);
                    }
                }
            }
        }, null, DevTools.EfxSupportedGames);
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
                    } else if (f.FieldType.IsEnum && !f.FieldType.IsEnumDefined(f.GetValue(a)!)) {
                        AddInconsistency(attrType, f.Name, new ukn(Convert.ToInt32(f.GetValue(a))).ToString(), filepath);
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
                HandleObjectValuePrint(dict, file, attr);
            }

            static void HandleObjectValuePrint(Dictionary<Type, Dictionary<string, Dictionary<EfxVersion, HashSet<string>>>> dict, EfxFile file, object target)
            {
                var targetType = target.GetType();
                if (targetType.Namespace?.StartsWith("System") == true) return;

                if (!dict.TryGetValue(targetType, out var allValues)) {
                    dict[targetType] = allValues = new();
                }

                var fieldInfos = targetType.IsValueType ? targetType.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!
                    : EfxTools.GetFieldInfo(targetType, file.Header!.Version)
                        .Select(pair => targetType.GetField(pair.name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!)
                        .Where(f => f != null);

                foreach (var fi in fieldInfos) {

                    if (!allValues.TryGetValue(fi.Name, out var values)) {
                        allValues[fi.Name] = values = new();
                    }
                    if (!values.TryGetValue(file.Header!.Version, out var fieldValues)) {
                        values[file.Header!.Version] = fieldValues = new();
                    }
                    var value = fi.GetValue(target);
                    if (value is int i) fieldValues.Add(new ukn(i).GetMostLikelyValueTypeString());
                    else if (value is uint u) fieldValues.Add(new ukn(u).GetMostLikelyValueTypeString());
                    else if (value is float f) fieldValues.Add(new ukn(f).GetMostLikelyValueTypeString());
                    else if (value is RszTool.via.Color c) fieldValues.Add(new ukn(c.rgba).GetMostLikelyValueTypeString());
                    else if (value is ukn uu) fieldValues.Add(uu.GetMostLikelyValueTypeString());
                    else {
                        fieldValues.Add(value?.ToString() ?? "NULL");
                        if (value != null && fi.FieldType.IsAssignableTo(typeof(BaseModel))) {
                            HandleObjectValuePrint(dict, file, value);
                        } else if (fi.FieldType.IsArray) {
                            foreach (var it in (Array)value!) { HandleObjectValuePrint(dict, file, it); }
                        } else if (fi.FieldType.IsGenericType && fi.FieldType.GetGenericTypeDefinition() == typeof(List<>)) {
                            foreach (var it in (System.Collections.IList)value!) { HandleObjectValuePrint(dict, file, it); }
                        }
                    }
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
