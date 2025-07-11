using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using ReeLib;
using Shouldly;

namespace ReaGE.Tests;

public partial class TestUvar : TestBase
{
    public TestUvar(Node testScene) : base(testScene) { }

    [Test]
    public async Task FullReadWriteTest()
    {
        var converter = new AssetConverter(GodotImportOptions.testImport);
        await ExecuteFullReadTest("uvar", async (game, filepath, stream) => {
            converter.Game = game;
            using var file = new UVarFile(new FileHandler(filepath));
            converter.Uvar.LoadFile(file);

            var imported = CreateTempResource(converter, converter.Uvar, filepath);
            (await converter.Uvar.Import(file, imported)).ShouldBe(true);

            using var exported = await ExportToMemory(converter.Uvar, imported, file.FileHandler.FileVersion);
            exported.ShouldNotBeNull();

            GetAllUvars(exported).Count().ShouldBe(GetAllUvars(file).Count());

            foreach (var (file_out, file_in) in PairEnumerate(GetAllUvars(exported), GetAllUvars(file))) {
                file_out.Header.variableCount.ShouldBe(file_in.Header.variableCount);
                file_out.Variables.Count.ShouldBe(file_in.Variables.Count);
                file_out.Header.embedCount.ShouldBe(file_in.Header.embedCount);
                file_out.EmbeddedUVARs.Count.ShouldBe(file_in.EmbeddedUVARs.Count);
                file_out.Header.name.ShouldBe(file_in.Header.name);
                file_out.HashData.Guids!.Length.ShouldBe(file_in.Header.variableCount);

                foreach (var (v1, v2) in PairEnumerate(file_out.Variables, file_in.Variables)) {
                    v1.Name.ShouldBe(v2.Name);
                    v1.nameHash.ShouldBe(v2.nameHash);
                    v1.flags.ShouldBe(v2.flags);
                    v1.guid.ShouldBe(v2.guid);
                    v1.Value.ShouldBeEquivalentTo(v2.Value);
                    (v1.Expression == null).ShouldBe(v2.Expression == null);

                    if (v1.Expression == null) continue;

                    v1.Expression.Connections.ShouldBeEquivalentTo(v2.Expression!.Connections);
                    foreach (var (n1, n2) in PairEnumerate(v1.Expression.Nodes, v2.Expression.Nodes)) {
                        n1.Name.ShouldBe(n2.Name);
                        n1.nodeId.ShouldBe(n2.nodeId);
                        n1.uknCount.ShouldBe(n2.uknCount);
                        n1.Parameters.Select(p => (p.nameHash, p.type, p.value)).ShouldBeEquivalentTo(n2.Parameters.Select(p => (p.nameHash, p.type, p.value)));
                    }
                }
            }
        });
    }

    // [Test]
    // public void VerifyData()
    // {
    //     var uvars = new HashSet<(ReeLib.UvarFile.Variable.TypeKind kind, ReeLib.UvarFile.UvarFlags flags, string val)>();
    //     var nodevars = new HashSet<(ReeLib.UvarFile.UvarExpression.NodeValueType type, string val)>();
    //     ExecuteFullReadTest("uvar", (game, filepath, stream) => {
    //         using var file = new UvarFile(new FileHandler(stream));
    //         file.Read();

    //         foreach (var uvar in GetAllUvars(file)) {
    //             foreach (var variable in uvar.Variables) {
    //                 if (variable.expression != null) {
    //                     foreach (var node in variable.expression.nodes) {
    //                         foreach (var vv in node.parameters) {
    //                             nodevars.Add((vv.type, vv.value?.ToString() ?? "NULL"));
    //                         }
    //                     }
    //                 }
    //                 var str = variable.value?.ToString() ?? "NULL";
    //                 uvars.Add((variable.type, variable.flags, str));
    //             }
    //         }

    //         // files won't always be binary exact equal - devs aren't sure whether they want to use the same string table entry for duplicate strings or not, even within the same file
    //         // example: RE2R SectionRoot\UserData\ropewayglobalvariables.uvar.2
    //         // BinaryTools.CompareFileOutput(filepath, file).ShouldBe(true);
    //     });
    //     File.WriteAllLines(Path.Combine(ReachForGodot.UserdataPath, "uvar_values.yml"),
    //         uvars.OrderBy(a => a.kind).Select(a => $"- {a.kind}: \"{a.val} [{a.flags}]\""));
    //     File.WriteAllLines(Path.Combine(ReachForGodot.UserdataPath, "uvar_node_values.yml"),
    //         nodevars.OrderBy(a => a.type).Select(a => "- " + a.type + ": \"" + a.val + "\""));
    // }

    private IEnumerable<UVarFile> GetAllUvars(UVarFile file)
    {
        yield return file;
        foreach (var e in file.EmbeddedUVARs.SelectMany(x => GetAllUvars(x))) {
            yield return e;
        }
    }
}
