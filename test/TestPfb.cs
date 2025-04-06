using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using GodotTestDriver;
using RszTool;
using Shouldly;

namespace ReaGE.Tests;

public partial class TestPfb : TestBase
{
    public TestPfb(Node testScene) : base(testScene) { }

    [Test]
    public async Task FullReadTest()
    {
        var converter = new AssetConverter(GodotImportOptions.testImport);
        await ExecuteFullReadTest("pfb", async (game, fileOption, filepath) => {
            converter.Game = game;
            using var file = new PfbFile(fileOption, new FileHandler(filepath));
            file.Read();
            file.SetupGameObjects();

            var node = new PrefabNode() { Asset = new AssetReference(PathUtils.FullToRelativePath(filepath, converter.AssetConfig)!) };
            await converter.Pfb.Import(file, node);
            converter.Pfb.Clear();
            node.QueueFree();
        }, new() {
            { SupportedGame.ResidentEvil2RT, 12 }
        }, SupportedGame.ResidentEvil2, SupportedGame.DevilMayCry5, SupportedGame.ResidentEvil3, SupportedGame.ResidentEvil7);
    }
}
