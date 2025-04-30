using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using RszTool;
using Shouldly;

namespace ReaGE.Tests;

public partial class TestCollisionFiles : TestBase
{
    public TestCollisionFiles(Node testScene) : base(testScene) { }

    [Test]
    public async Task CdefReadTest()
    {
        var converter = new AssetConverter(GodotImportOptions.testImport);
        await ExecuteFullReadTest("cdef", async (game, fileOption, filepath) => {
            using var file = new CdefFile(new FileHandler(filepath));
            file.Read();

            file.Masks.Select(p => p.padding1).ShouldAllBe(n => n == 0);
            file.Masks.Select(p => p.padding2).ShouldAllBe(n => n == 0);

            // file.Materials.Select(p => p.padding1).ShouldAllBe(n => n == 0);
            // file.Materials.Select(p => p.padding2).ShouldAllBe(n => n == 0);

            file.Attributes.Select(p => p.padding).ShouldAllBe(n => n == 0);

            file.Presets.Select(p => p.padding1).ShouldAllBe(n => n == 0);
            file.Presets.Select(p => p.padding2).ShouldAllBe(n => n == 0);

            var res = new CollisionDefinitionResource();
            (await converter.Cdef.Import(file, res)).ShouldBe(true);
            using var exportFile = converter.Cdef.CreateFile(new MemoryStream(), file.FileHandler.FileVersion);
            // (await converter.Cdef.Export(res, exportFile)).ShouldBe(true);
        });
    }

    [Test]
    public async Task CfilReadTest()
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

            exportFile.LayerGuid.ShouldBe(file.LayerGuid);
            exportFile.Masks.ShouldBeEquivalentTo(file.Masks);
        });
    }

    [Test]
    public async Task CmatReadTest()
    {
        var converter = new AssetConverter(GodotImportOptions.testImport);
        await ExecuteFullReadTest("cmat", async (game, fileOption, filepath) => {
            using var file = new CmatFile(new FileHandler(filepath));
            file.Read();
            var res = new CollisionMaterialResource();
            (await converter.Cmat.Import(file, res)).ShouldBe(true);
            res.MaterialGuid.ShouldBe(file.materialGuid);
            res.AttributeGuids.ShouldBeEquivalentTo(file.Attributes);
        });
    }

    [Test]
    public async Task CollisionHFReadTest()
    {
        var converter = new AssetConverter(GodotImportOptions.testImport);
        await ExecuteFullReadTest("chf", async (game, fileOption, filepath) => {
            using var file = new CHFFile(new FileHandler(filepath));
            file.Read();
        });
    }

    [Test]
    public async Task HFReadTest()
    {
        var converter = new AssetConverter(GodotImportOptions.testImport);
        await ExecuteFullReadTest("hf", async (game, fileOption, filepath) => {
            using var file = new HFFile(new FileHandler(filepath));
            file.Read();
        });
    }
}
