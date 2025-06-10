using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using RszTool;

namespace ReaGE.Tests;

public partial class TestNavigationFiles : TestBase
{
    public TestNavigationFiles(Node testScene) : base(testScene) { }

    private static AimpFile HandleAimpFile(SupportedGame game, RszFileOption fileOption, string filepath)
    {
        var file = new AimpFile(fileOption, new FileHandler(filepath));
        file.Read();
        return file;
    }

    [Test]
    public async Task NavmeshReadTest()
    {
        await ExecuteFullReadTest("ainvm", (game, fileOption, filepath) => {
            using var file = HandleAimpFile(game, fileOption, filepath);
        });
    }

    [Test]
    public async Task MapPointReadTest()
    {
        await ExecuteFullReadTest("aimap", (game, fileOption, filepath) => {
            using var file = HandleAimpFile(game, fileOption, filepath);
        });
    }

    [Test]
    public async Task WaypointReadTest()
    {
        await ExecuteFullReadTest("aiwayp", (game, fileOption, filepath) => {
            using var file = HandleAimpFile(game, fileOption, filepath);
        });
    }

    [Test]
    public async Task VolumeSpaceReadTest()
    {
        await ExecuteFullReadTest("aivspc", (game, fileOption, filepath) => {
            using var file = HandleAimpFile(game, fileOption, filepath);
        });
    }

    [Test]
    public async Task WaypointManagerReadTest()
    {
        await ExecuteFullReadTest("aiwaypmgr", (game, fileOption, filepath) => {
            using var file = HandleAimpFile(game, fileOption, filepath);
        });
    }

    [Test]
    public async Task NavmeshManagerReadTest()
    {
        await ExecuteFullReadTest("ainvmmgr", (game, fileOption, filepath) => {
            using var file = HandleAimpFile(game, fileOption, filepath);
        });
    }
}
