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
        await ExecuteFullReadTest("ainvm", async (game, fileOption, filepath) => {
            using var file = HandleAimpFile(game, fileOption, filepath);
        });
    }

    [Test]
    public async Task MapPointReadTest()
    {
        await ExecuteFullReadTest("aimap", async (game, fileOption, filepath) => {
            using var file = HandleAimpFile(game, fileOption, filepath);
        });
    }

    [Test]
    public async Task WaypointReadTest()
    {
        await ExecuteFullReadTest("aiwayp", async (game, fileOption, filepath) => {
            using var file = HandleAimpFile(game, fileOption, filepath);
        });
    }

    [Test]
    public async Task VolumeSpaceReadTest()
    {
        await ExecuteFullReadTest("aivspc", async (game, fileOption, filepath) => {
            using var file = HandleAimpFile(game, fileOption, filepath);
        });
    }

    [Test]
    public async Task WaypointManagerReadTest()
    {
        await ExecuteFullReadTest("aiwaypmgr", async (game, fileOption, filepath) => {
            using var file = HandleAimpFile(game, fileOption, filepath);
        });
    }

    [Test]
    public async Task NavmeshManagerReadTest()
    {
        await ExecuteFullReadTest("ainvmmgr", async (game, fileOption, filepath) => {
            using var file = HandleAimpFile(game, fileOption, filepath);
        });
    }
}
