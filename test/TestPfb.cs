using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using ReeLib;

namespace ReaGE.Tests;

public partial class TestPfb : TestBase
{
    public TestPfb(Node testScene) : base(testScene) { }

    [Test]
    public async Task FullReadTest()
    {
        var converter = new AssetConverter(GodotImportOptions.testImport);
        await ExecuteFullReadTest("pfb", async (game, filepath, stream) => {
            converter.Game = game;
            using var file = new PfbFile(converter.FileOption, new FileHandler(stream, filepath));
            file.Read();
            file.SetupGameObjects();

            var node = new PrefabNode() { Asset = new AssetReference(PathUtils.FullToRelativePath(filepath, converter.AssetConfig)!) };
            await converter.Pfb.Import(file, node);
            converter.Pfb.Clear();
            converter.Context.Clear();
            node.QueueFree();
        }, new() {
            { SupportedGame.ResidentEvil2, 12 },
            { SupportedGame.ResidentEvil2RT, 12 }
        });
    }
}
