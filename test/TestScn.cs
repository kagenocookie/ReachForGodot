using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
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
            var version = file.FileHandler.FileVersion;
            if (version == 19) {
                file.GameObjectInfoList.ShouldAllBe(i => i.ukn == -1);
            } else if (version == 20) {
                file.GameObjectInfoList.ShouldAllBe(i => i.ukn == 0);
            }

            var node = new SceneFolder() { Asset = new AssetReference(PathUtils.FullToRelativePath(filepath, converter.AssetConfig)!) };
            await converter.Scn.Import(file, node);
            converter.Scn.Clear();
            converter.Context.Clear();
            node.Free();
        }, new () {
            { SupportedGame.DevilMayCry5, 21 }, // some empty strings are weird and have an extra 4 bytes after them - RSZ dump issue?
            { SupportedGame.DragonsDogma2, 1 }, // appsystem\scene\maincamera.scn.20
            { SupportedGame.ResidentEvil4, 26 }, // 25 gimmicks and 1 level are currently expected to fail (outdated and unused files?)
        });
    }
}
