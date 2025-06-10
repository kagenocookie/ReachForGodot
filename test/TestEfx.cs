using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using RszTool;
using RszTool.Efx;
using RszTool.Efx.Structs.Basic;
using RszTool.Efx.Structs.Common;
using RszTool.Efx.Structs.Field;
using RszTool.Efx.Structs.Main;
using RszTool.Efx.Structs.Misc;
using RszTool.Efx.Structs.Transforms;
using RszTool.Tools;
using Shouldly;

using Ukn = RszTool.UndeterminedFieldType;

namespace ReaGE.Tests;

public partial class TestEfx : TestBase
{
    public TestEfx(Node testScene) : base(testScene) { }

    // file notes: tests with commented out [Test] attributes are more of a RszTool efx struct development assistance thing

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

    // [Test]
    public async Task ExpressionNameEvaluation()
    {
        var matches = DevTools.FindEfxWhere(efx =>
            efx.Entries.Any(e => e.Attributes.OfType<IExpressionAttribute>().Any()))
            .SelectMany(f => GetAllExpressions(f))
            .Where(set => set.expression.parameters.Count > 0)
            .OrderBy(a => a.attr.GetType().Name)
            .Select(pair => $"{pair.path} : {pair.attr.GetType().Name.Replace("EFXAttribute", "")} {pair.attr.ExpressionBits} => {pair.expression}");

        static IEnumerable<(string path, IExpressionAttribute attr, EFXExpressionTree expression)> GetAllExpressions(EfxFile file) {
            file.ParseExpressions();
            return file.GetAttributesAndActions(true)
                // .Where(attr => attr is IExpressionAttribute)
                // .Select(attr => (attr.))
                .OfType<IExpressionAttribute>()
                .Where(attr => attr is
                    // fully resolved
                    not EFXAttributeTransform2DExpression
                    and not EFXAttributeFadeByDepthExpression
                    and not EFXAttributeTransform3DExpression
                    and not EFXAttributePtTransform3DExpression
                    and not EFXAttributeFadeByAngleExpression
                    and not EFXAttributeSpawnExpression
                    and not EFXAttributeLifeExpression

                    // mostly resolved
                    and not EFXAttributeTypeNodeBillboardExpression
                    and not EFXAttributeTypeGpuBillboardExpression
                    and not EFXAttributeEmitterShape3DExpression
                    and not EFXAttributeAttractorExpression

                    // needs bitset versioning
                    and not EFXAttributeTypeBillboard3DExpression
                    and not EFXAttributeTypeMeshExpression
                    and not EFXAttributeTypeNoDrawExpression
                    and not EFXAttributeTypeRibbonLengthExpression
                    and not EFXAttributeShaderSettingsExpression

                    // unresolveable - no additional reliable info in shipped files of the games I own; needs ingame testing or alternative solutions
                    and not EFXAttributeTypeRibbonChainExpression
                    and not EFXAttributeNoiseExpression
                    and not EFXAttributeEmitterShape2DExpression
                    and not EFXAttributePtAngularVelocity2DExpression
                    and not EFXAttributeUnitCullingExpression
                    and not EFXAttributeTypeLightning3DExpression
                    and not EFXAttributeTypeRibbonParticleExpression
                    and not EFXAttributeTypeRibbonFollowMaterialExpression
                    and not EFXAttributeTypeRibbonLengthMaterialExpression
                    and not EFXAttributeTypePolygonTrailMaterialExpression
                    and not EFXAttributeTypeGpuRibbonLengthExpression
                    and not EFXAttributeTypeStrainRibbonExpression
                    and not EFXAttributeTypeGpuPolygonExpression
                    and not EFXAttributeProceduralDistortionExpression
                    and not EFXAttributeMeshEmitterExpression
                    and not EFXAttributeTypeLightning3DMaterialExpression
                    and not EFXAttributeTypeBillboard3DMaterialExpression
                    and not EFXAttributeTypeRibbonFollowExpression
                    and not EFXAttributeTypePolygonExpression
                    and not EFXAttributeVelocity3DExpression
                    and not EFXAttributeRgbCommonExpression
                    and not EFXAttributeRotateAnimExpression
                    and not EFXAttributeUVSequenceExpression
                    and not EFXAttributeVelocity2DExpression
                    and not EFXAttributeVanishArea3DExpression
                    and not EFXAttributeScaleAnimExpression
                    and not EFXAttributeTypeGpuRibbonFollowExpression
                    and not EFXAttributeTypeRibbonFixEndExpression
                    and not EFXAttributeTypeStrainRibbonMaterialExpression
                    and not EFXAttributeTypeBillboard2DExpression
                    and not EFXAttributeVectorFieldParameterExpression // figure out name for hash ext:2031344731

                    // barely resolved
                    and not EFXAttributeDistortionExpression
                    and not EFXAttributeTypeGpuMeshExpression
                )
                .SelectMany(attr => attr.Expression?.ParsedExpressions?
                    .Select(expr => (file.FileHandler.FilePath!, attr, expr))
                    ?? Enumerable.Empty<(string, IExpressionAttribute, EFXExpressionTree)>()
                );
        }

        GD.Print(string.Join("\n", matches));
        await DumpEfxAttributeUsageList();
        await DumpEfxStructValueLists();
    }

    // [Test]
    public void ClipStructLookups()
    {
        var matches = DevTools.FindEfxWhere(efx => true)
            .SelectMany(f => GetAllClips(f))
            .Where(set => set.attr.Clip.interpolationDataCount > 0)
            // .Where(set => set.attr.Clip.frames?.Any(f => f.type == FrameInterpolationType.Type13) == true && set.attr.Clip.interpolationData?.Any() == true)
            // .Where(set => set.attr.Clip.interpolationDataCount == 0 && true == set.attr.Clip.frames?.Any(f => f.type == FrameInterpolationType.Type5))
            .OrderBy(a => a.attr.GetType().Name)
            // .Select(pair => $"{pair.path} : {pair.attr.GetType().Name.Replace("EFXAttribute", "")} {pair.attr.ClipBits} => {pair.attr.Clip}")
            .Select(pair => $"{pair.path} : {pair.attr.GetType().Name.Replace("EFXAttribute", "")} {string.Join(" | ", pair.attr.Clip.clips!)}")
            ;

        static IEnumerable<(string path, IClipAttribute attr)> GetAllClips(EfxFile file) {
            return file.GetAttributesAndActions(true)
                .OfType<IClipAttribute>()
                .SelectMany(attr => attr.Clip.clips?
                    .Select(expr => (file.FileHandler.FilePath!, attr))
                    ?? Enumerable.Empty<(string, IClipAttribute)>()
                );
        }

        GD.Print(string.Join("\n", matches));
    }

    [Test]
    public async Task FullReadWriteTest()
    {
        Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

        // var actionContainingEfx = DevTools.FindEfxWhere((efx) => efx.UvarGroups.Any(ug => ug.uvarType > 1));
        // var actionContainingEfx = DevTools.FindEfxWhere((efx, success) =>
        //     efx.Entries
        //         .Any(e =>
        //             e.Attributes.Any(a => a is EFXAttributeUnknownDD2_239) &&
        //             !e.Attributes.Any(a => a is EFXAttributeEffectOptimizeShader || a is EFXAttributeShaderSettings)
        //         )
        //     );

        // GD.Print(string.Join("\n", actionContainingEfx.Select(t => t.FileHandler.FilePath + " : " + t.UvarGroups[0].uvarType +" + " + t.UvarGroups[1].uvarType)));

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
        // var matches = DevTools.FindEfxAttributes<IMaterialClipAttribute>(clipAttr => clipAttr.MaterialClip.clipCount != clipAttr.MaterialClip.mdfPropertyCount && clipAttr.MaterialClip.mdfPropertyCount != 0);
        // var matches = DevTools.FindEfxAttributes<EFXAttributeUnknownRE4_228>(attr => attr.unkn20.value != 0);
        // var matches = DevTools.FindEfxAttributes<EFXAttributeTypeLightning3DV1>(attr => true);
        // var matches = DevTools.FindEfxAttributes<EFXAttributeTextureUnitExpression>(a => true);
        // var matches = DevTools.FindEfxAttributes<EFXAttributeTypeMeshClip>(attr => attr.mdfPropertyCount > 0);
        // var matches = DevTools.FindEfxAttributes<EFXAttributeSpawn>(attr => attr.sb_unkn3 > 1, SupportedGame.DragonsDogma2);
        // var matches = DevTools.FindEfxByAttributeType<EFXAttributeUnitCulling>(EfxAttributeType.UnitCulling);

        // GD.Print(string.Join("\n", matches));
        // GD.Print(string.Join("\n", matches.Select(t => t.FileHandler.FilePath).Distinct()));
        // GD.Print(string.Join("\n", matches.Select(t => t.file.FileHandler.FilePath + ": " + t.matchedAttribute.unknBitFlag + ", " + t.matchedAttribute.flags + " => " + t.matchedAttribute.unkn2_14.GetMostLikelyValueTypeString()).Distinct()));

        // Debug.Break();

        // await FullExpressionParseTest();
        await FullReadTest();
        await DumpEfxAttributeUsageList();
        // await DumpEfxStructValueLists<EFXAttributeSpawn>();
        await DumpEfxStructValueLists();

        // var unknownHashes = EfxFile.UnknownParameterHashes;
        // var foundHashes = EfxFile.FoundNamedParameterHashes;
        // var eznames = unknownHashes.Select(hash => foundHashes.TryGetValue(hash, out var name) ? $"{hash}={name}" : null).Where(x => x != null).ToArray();

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
        await ExecuteFullReadTest("efx", (game, fileOption, filepath) => {
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
        await ExecuteFullReadTest("efx", (game, fileOption, filepath) => {
            using var file = new EfxFile(new FileHandler(filepath));
            try {
                file.Read();
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
                    var tree = EfxExpressionStringParser.Parse(originalStr, parsed.parameters);
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
        await ExecuteFullReadTest("efx", (game, fileOption, filepath) => {
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
                    if (f.FieldType == typeof(Ukn) && f.GetValue(a) is Ukn uu && uu.value != 0) {
                        AddInconsistency(attrType, f.Name, uu.ToString(), filepath);
                    } else if (f.FieldType == typeof(int) && f.GetValue(a) is int ii && LooksLikeFloat(ii)) {
                        AddInconsistency(attrType, f.Name, BitConverter.Int32BitsToSingle(ii).ToString("0.0#"), filepath);
                    } else if (f.FieldType == typeof(uint) && f.GetValue(a) is uint n && LooksLikeFloat((int)n) && !f.Name.Contains("hash", StringComparison.OrdinalIgnoreCase) && !f.Name.Contains("mask", StringComparison.OrdinalIgnoreCase)) {
                        AddInconsistency(attrType, f.Name, BitConverter.Int32BitsToSingle((int)n).ToString("0.0#"), filepath);
                    } else if (f.FieldType == typeof(float) && f.GetValue(a) is float flt &&
                        (Math.Abs(flt) > 100000000 && !float.IsInfinity(flt) || flt != 0 && BitConverter.SingleToUInt32Bits(flt) < 1000)
                    ) {
                        AddInconsistency(attrType, f.Name, new Ukn(flt).ToString(), filepath);
                    } else if (f.FieldType.IsEnum && !f.FieldType.IsEnumDefined(f.GetValue(a)!)) {
                        AddInconsistency(attrType, f.Name, new Ukn(Convert.ToInt32(f.GetValue(a))).ToString(), filepath);
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
        await ExecuteFullReadTest("efx", (game, fileOption, filepath) => {
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
                usageSb.Append($"{EfxAttributeTypeRemapper.ToAttributeTypeID(version, attr)} = \t").Append(attr.ToString()).AppendLine($" ({paths.Count})");
            }


            usageSb.AppendLine();
            usageSb.AppendLine("Unmapped:");
            foreach (var val in Enum.GetValues<EfxAttributeType>()) {
                if (!EfxAttributeTypeRemapper.HasAttributeType(version, val)) {
                    usageSb.Append(val).Append(" = {").Append(string.Join(", ", val.GetVersionsOfType().Select((verId => verId.version + " = " + verId.typeId)))).AppendLine(" }");
                }
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
        await ExecuteFullReadTest("efx", (game, fileOption, filepath) => {
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
                    if (value is int i) fieldValues.Add(new Ukn(i).GetMostLikelyValueTypeString());
                    else if (value is uint u) fieldValues.Add(new Ukn(u).GetMostLikelyValueTypeString());
                    else if (value is float f) fieldValues.Add(new Ukn(f).GetMostLikelyValueTypeString());
                    else if (value is RszTool.via.Color c) fieldValues.Add(new Ukn(c.rgba).GetMostLikelyValueTypeString());
                    else if (value is Ukn uu) fieldValues.Add(uu.GetMostLikelyValueTypeString());
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
