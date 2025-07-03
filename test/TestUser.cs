using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using ReeLib;

namespace ReaGE.Tests;

public partial class TestUser : TestBase
{
    public TestUser(Node testScene) : base(testScene) { }

    [Test]
    public async Task FullReadTest()
    {
        var converter = new AssetConverter(GodotImportOptions.testImport);
        await ExecuteFullReadTest("user", (game, filepath, stream) => {
            converter.Game = game;
            using var file = new UserFile(converter.FileOption, new FileHandler(stream, filepath));
            file.Read();
        });
    }
}
