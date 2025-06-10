using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using RszTool;

namespace ReaGE.Tests;

public partial class TestMcol : TestBase
{
    public TestMcol(Node testScene) : base(testScene) { }

    [Test]
    public async Task ReadTest()
    {
        await ExecuteFullReadTest("mcol", (game, fileOption, filepath) => {
            var file = new McolFile(new FileHandler(filepath));
            file.Read();
        }, null, GamesExcept(SupportedGame.ResidentEvil7));
    }
}
