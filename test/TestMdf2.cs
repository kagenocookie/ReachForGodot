using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using RszTool;
using Shouldly;

namespace ReaGE.Tests;

public partial class TestMdf2 : TestBase
{
    public TestMdf2(Node testScene) : base(testScene) { }

    [Test]
    public async Task Mdf2ReadTest()
    {
        var converter = new AssetConverter(GodotImportOptions.testImport);
        await ExecuteFullReadTest("mdf2", async (game, fileOption, filepath) => {
            converter.Game = game;
            using var file = new MdfFile(fileOption, new FileHandler(filepath));
            file.Read().ShouldBe(true);

            var res = new MaterialDefinitionResource();
            (await converter.Mdf2.Import(file, res)).ShouldBeTrue();

            using var exportFile = await ExportToMemory(converter.Mdf2, res, file.FileHandler.FileVersion);
            exportFile.ShouldNotBeNull();

            exportFile.Header.Data.ShouldBeEquivalentTo(file.Header.Data);

            foreach (var (file_out, file_in) in PairEnumerate(exportFile.MatDatas, file.MatDatas)) {
                file_out.Header.matName.ShouldBe(file_in.Header.matName);
                file_out.Header.matNameHash.ShouldBe(file_in.Header.matNameHash);
                file_out.Header.mmtrPath.ShouldBe(file_in.Header.mmtrPath, StringCompareShould.IgnoreCase);
                file_out.Header.alphaFlags.ShouldBe(file_in.Header.alphaFlags);
                file_out.Header.gpbfDataCount.ShouldBe(file_in.Header.gpbfDataCount);
                file_out.Header.gpbfNameCount.ShouldBe(file_in.Header.gpbfNameCount);
                file_out.Header.paramCount.ShouldBe(file_in.Header.paramCount);

                foreach (var (p_out, p_in) in PairEnumerate(file_out.ParamHeaders, file_in.ParamHeaders)) {
                    p_out.asciiHash.ShouldBe(p_in.asciiHash);
                    p_out.componentCount.ShouldBe(p_in.componentCount);
                    p_out.parameter.ShouldBe(p_in.parameter);
                    p_out.paramName.ShouldBe(p_in.paramName);
                }

                foreach (var (tex_out, tex_in) in PairEnumerate(file_out.TexHeaders, file_in.TexHeaders)) {
                    tex_out.asciiHash.ShouldBe(tex_in.asciiHash);
                    tex_out.hash.ShouldBe(tex_in.hash);
                    tex_out.texPath.ShouldBe(tex_in.texPath, StringCompareShould.IgnoreCase);
                }
            }
            converter.Context.Clear();
        });
    }
}
