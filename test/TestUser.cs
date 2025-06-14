using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using RszTool;

namespace ReaGE.Tests;

public partial class TestUser : TestBase
{
    public TestUser(Node testScene) : base(testScene) { }

    [Test]
    public async Task FullReadTest()
    {
        var converter = new AssetConverter(GodotImportOptions.testImport);
        await ExecuteFullReadTest("user", (game, fileOption, filepath) => {
            converter.Game = game;
            using var file = new UserFile(fileOption, new FileHandler(filepath));
            file.Read();
        });
    }
}
