using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using GodotTestDriver;
using RszTool;
using Shouldly;

namespace ReaGE.Tests;

public partial class TestCfil : TestBase
{
    public TestCfil(Node testScene) : base(testScene) { }

    [Test]
    public async Task FullReadTest()
    {
        var converter = new AssetConverter(GodotImportOptions.testImport);
        await ExecuteFullReadTest("cfil", async (game, fileOption, filepath) => {
            using var file = new CfilFile(new FileHandler(filepath));
            file.Read();

            (file.ukn3 + file.ukn4 + file.ukn5 + file.ukn6).ShouldBe(0, "Found unhandled CFIL bytes");
            (file.uknOffset >= file.guidListOffset + sizeof(long) * 2 * file.guidCount).ShouldBeTrue("Found unknown data in CFIL");

            var res = new CollisionFilterResource();
            (await converter.Cfil.Import(file, res)).ShouldBe(true);
            using var exportFile = converter.Cfil.CreateFile(new MemoryStream(), file.FileHandler.FileVersion);
            (await converter.Cfil.Export(res, exportFile)).ShouldBe(true);

            exportFile.myGuid.ShouldBe(file.myGuid);
            exportFile.Guids.ShouldBeEquivalentTo(file.Guids);
            res.Free();
        });
    }
}
