using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using GodotTestDriver;
using RszTool;
using Shouldly;

namespace ReaGE.Tests;

public partial class TestScn : TestBase
{
    public TestScn(Node testScene) : base(testScene) { }

    [Test]
    public async Task FullReadTest()
    {
        var converter = new AssetConverter(GodotImportOptions.testImport);
        await ExecuteFullReadTest("scn", async (game, fileOption, filepath) => {
            converter.Game = game;
            using var file = new ScnFile(fileOption, new FileHandler(filepath));
            file.Read();
            file.SetupGameObjects();

            var node = new SceneFolder() { Asset = new AssetReference(PathUtils.FullToRelativePath(filepath, converter.AssetConfig)!) };
            await converter.Scn.Import(file, node);
            converter.Scn.Clear();
            node.QueueFree();
        }, new () {
            { SupportedGame.DragonsDogma2, 1 }, // appsystem\scene\maincamera.scn.20
            { SupportedGame.ResidentEvil4, 26 }, // 25 gimmicks and 1 level are currently expected to fail
        }, SupportedGame.ResidentEvil2, SupportedGame.DevilMayCry5, SupportedGame.ResidentEvil3, SupportedGame.ResidentEvil7);
    }
}
