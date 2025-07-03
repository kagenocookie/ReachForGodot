using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using ReeLib;
using Shouldly;

namespace ReaGE.Tests;

public partial class TestCollisionFiles : TestBase
{
    public TestCollisionFiles(Node testScene) : base(testScene) { }

    [Test]
    public async Task CdefReadTest()
    {
        var converter = new AssetConverter(GodotImportOptions.testImport);
        await ExecuteFullReadTest("cdef", async (game, filepath, stream) => {
            using var file = new CdefFile(new FileHandler(stream, filepath));
            file.Read();

            file.Masks.Select(p => p.padding1).ShouldAllBe(n => n == 0);
            file.Masks.Select(p => p.padding2).ShouldAllBe(n => n == 0);

            file.Attributes.Select(p => p.padding).ShouldAllBe(n => n == 0);

            var res = new CollisionDefinitionResource();
            (await converter.Cdef.Import(file, res)).ShouldBe(true);
            using var exportFile = converter.Cdef.CreateFile(new MemoryStream(), file.FileHandler.FileVersion);
            // (await converter.Cdef.Export(res, exportFile)).ShouldBe(true);
        });
    }

    [Test]
    public async Task DefReadTest()
    {
        var converter = new AssetConverter(GodotImportOptions.testImport);
        await ExecuteFullReadTest("def", (game, filepath, stream) => {
            using var file = new DefFile(new FileHandler(stream, filepath));
            file.Read();
        });
    }

    [Test]
    public async Task CfilReadTest()
    {
        var converter = new AssetConverter(GodotImportOptions.testImport);
        await ExecuteFullReadTest("cfil", async (game, filepath, stream) => {
            using var file = new CfilFile(new FileHandler(stream, filepath));
            file.Read();

            (file.uknOffset == 0 || file.uknOffset == file.FileHandler.Position).ShouldBeTrue("Found unknown data in CFIL");

            var res = new CollisionFilterResource();
            (await converter.Cfil.Import(file, res)).ShouldBe(true);
            using var exportFile = converter.Cfil.CreateFile(new MemoryStream(), file.FileHandler.FileVersion);
            (await converter.Cfil.Export(res, exportFile)).ShouldBe(true);
            if (file.FileHandler.FileVersion >= 7)
            {
                exportFile.LayerGuid.ShouldBe(file.LayerGuid);
                exportFile.MaskGuids.ShouldBeEquivalentTo(file.MaskGuids);
            }
            else
            {
                // no point in adding real data support for these right now (RE7 uses index instead of guids)
                // if ever needed, the solution will be to fetch the guid from the cdef file on Godot side to make it consistent
            }
        });
    }

    [Test]
    public async Task CmatReadTest()
    {
        var converter = new AssetConverter(GodotImportOptions.testImport);
        await ExecuteFullReadTest("cmat", async (game, filepath, stream) => {
            using var file = new CmatFile(new FileHandler(stream, filepath));
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
        await ExecuteFullReadTest("chf", (game, filepath, stream) => {
            using var file = new CHFFile(new FileHandler(stream, filepath));
            file.Read();
        });
    }

    [Test]
    public async Task HFReadTest()
    {
        var converter = new AssetConverter(GodotImportOptions.testImport);
        await ExecuteFullReadTest("hf", (game, filepath, stream) => {
            using var file = new HFFile(new FileHandler(stream, filepath));
            file.Read();
        });
    }
}
