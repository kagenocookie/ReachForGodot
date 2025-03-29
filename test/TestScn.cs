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
    public void FullReadTest()
    {
        ExecuteFullReadTest("scn", (game, fileOption, filepath) => {
            using var file = new ScnFile(fileOption, new FileHandler(filepath));
            file.Read();
            file.SetupGameObjects();
        }, new () {
            { SupportedGame.ResidentEvil4, 26 } // 25 gimmicks and 1 level are expected to fail
        }, SupportedGame.ResidentEvil2, SupportedGame.DevilMayCry5, SupportedGame.ResidentEvil3, SupportedGame.ResidentEvil7);
    }
}
