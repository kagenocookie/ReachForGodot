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
    public void FullReadTest()
    {
        ExecuteFullReadTest("pfb", (game, fileOption, filepath) => {
            using var file = new PfbFile(fileOption, new FileHandler(filepath));
            file.Read();
            file.SetupGameObjects();
        }, new() {
            { SupportedGame.ResidentEvil2RT, 12 }
        }, SupportedGame.ResidentEvil2, SupportedGame.DevilMayCry5, SupportedGame.ResidentEvil3, SupportedGame.ResidentEvil7);
    }
}
