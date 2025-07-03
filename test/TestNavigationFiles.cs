using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using ReeLib;

namespace ReaGE.Tests;

public partial class TestNavigationFiles : TestBase
{
    public TestNavigationFiles(Node testScene) : base(testScene) { }

    private static readonly HashSet<string> AimpCombinations = [];

    private static HashSet<(float, float)> boundsUkns = new();
    private static HashSet<(int, string)> uknIDs = new();

    private static AimpFile HandleAimpFile(SupportedGame game, string filepath, Stream stream)
    {
        var fileOption = ReachForGodot.GetAssetConfig(game).Workspace.RszFileOption;
        var file = new AimpFile(fileOption, new FileHandler(stream, filepath));
        file.Read();
        var ext = PathUtils.GetFilenameExtensionWithoutSuffixes(filepath).ToString();

        string? s1 = null, s2 = null;
        if (file.mainContent?.contents != null) {
            s1 = string.Join('+', file.mainContent.contents.Select(c => c.GetType().Name));
        }
        if (file.secondaryContent?.contents != null) {
            s2 = string.Join('+', file.secondaryContent.contents.Select(c => c.GetType().Name));
        }
        AimpCombinations.Add(game.ToShortName() + " " + ext + ": " + (s1 ?? "NONE") + " && " + (s2 ?? "NONE"));
        if (file.mainContent?.float1 != null) {
            boundsUkns.Add((file.mainContent.float1, file.mainContent.float2));
        }
        if (file.secondaryContent?.float1 != null) {
            boundsUkns.Add((file.secondaryContent.float1, file.secondaryContent.float2));
        }

        if (file.Header.uknId != 1) {
            uknIDs.Add((file.Header.uknId, filepath));
        }
        return file;
    }

    private static AimpFile TestSpecificFile(SupportedGame game, string filepath)
    {
        var conv = new AssetConverter(game, GodotImportOptions.directImport);
        var file = new AimpFile(conv.FileOption, new FileHandler(filepath));
        file.Read();
        return file;
    }

    [Test]
    public async Task NavmeshReadTest()
    {
        await ExecuteFullReadTest("ainvm", (game, filepath, stream) => {
            using var file = HandleAimpFile(game, filepath, stream);
        });
    }

    [Test]
    public async Task AIMapReadTest()
    {
        await ExecuteFullReadTest("aimap", (game, filepath, stream) => {
            using var file = HandleAimpFile(game, filepath, stream);
        });
    }

    [Test]
    public async Task WaypointReadTest()
    {
        await ExecuteFullReadTest("aiwayp", (game, filepath, stream) => {
            using var file = HandleAimpFile(game, filepath, stream);
        });
    }

    [Test]
    public async Task VolumeSpaceReadTest()
    {
        await ExecuteFullReadTest("aivspc", (game, filepath, stream) => {
            using var file = HandleAimpFile(game, filepath, stream);
        });
    }

    [Test]
    public async Task WaypointManagerReadTest()
    {
        await ExecuteFullReadTest("aiwaypmgr", (game, filepath, stream) => {
            using var file = HandleAimpFile(game, filepath, stream);
        });
    }

    [Test]
    public async Task NavmeshManagerReadTest()
    {
        await ExecuteFullReadTest("ainvmmgr", (game, filepath, stream) => {
            using var file = HandleAimpFile(game, filepath, stream);
        });

        GD.Print("AimpCombinations:\n" + string.Join("\n", AimpCombinations));
        // GD.Print("BoundsFloatsCombinations:\n" + string.Join("\n", boundsUkns));
        GD.Print("Header uknIDs:\n" + string.Join("\n", uknIDs));
    }
}
