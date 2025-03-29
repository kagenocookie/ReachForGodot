using System.Diagnostics;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using GodotTestDriver;
using RszTool;
using Shouldly;

namespace ReaGE.Tests;

public abstract partial class TestBase : TestClass
{
    protected Fixture _fixture = default!;

    public TestBase(Node testScene) : base(testScene) { }

    [SetupAll]
    public virtual Task Setup()
    {
        _fixture = new Fixture(TestScene.GetTree());
        return Task.CompletedTask;
    }

    [CleanupAll]
    public virtual void Cleanup() => _fixture.Cleanup();

    protected static AssetConfig GetConfig(SupportedGame game)
    {
        return ReachForGodot.GetAssetConfig(game);
    }

    protected static (string, RszFileOption) ResolveRszFile(SupportedGame game, string filepath)
    {
        var path = PathUtils.FindSourceFilePath(filepath, GetConfig(game));
        path.ShouldNotBeNullOrEmpty();
        var opt = TypeCache.CreateRszFileOptions(GetConfig(game));
        return (path, opt);
    }

    protected static void ExecuteFullReadTest(
        string extension,
        Action<SupportedGame, RszFileOption, string> action,
        Dictionary<SupportedGame, int>? expectedFails = null,
        params SupportedGame[] skipGames)
    {
        var sw = new Stopwatch();
        sw.Start();
        var configured = 0;
        var unconfigured = 0;
        var fails = new Dictionary<SupportedGame, int>();
        var gamelist = ReachForGodot.GameList.Except(skipGames);
        foreach (var game in gamelist) {
            var config = ReachForGodot.GetAssetConfig(game);
            if (!config.IsValid) {
                unconfigured++;
                continue;
            }

            var (successCount, failedCount) = ReachForGodotPlugin.ExecuteOnAllSourceFiles(game, extension, action);
            if (successCount + failedCount == 0) {
                if (!File.Exists(config.Paths.FilelistPath) || config.Paths.PakFiles.Length == 0) {
                    unconfigured++;
                    continue;
                }

                if (!PathUtils.GetFilesByExtensionFromListFile(config.Paths.FilelistPath, PathUtils.AppendFileVersion($".{extension}", config), null).Any()) {
                    // game does not use this file format
                    continue;
                }
            }

            var expected = expectedFails?.GetValueOrDefault(game) ?? 0;
            successCount.ShouldBeGreaterThan(0);
            fails[game] = failedCount;
            configured++;
        }

        GD.Print("");
        GD.Print($"Finished {extension} read test on {configured} out of {configured+unconfigured} supported games in {sw.Elapsed}");
        GD.Print("");
        int unexpectedFailCount = 0;
        foreach (var (game, failedCount) in fails) {
            var expected = expectedFails?.GetValueOrDefault(game) ?? 0;
            if (failedCount != expected) {
                unexpectedFailCount++;
                GD.PrintErr($"{game} {extension} read failed for {failedCount} files instead of expected {expected}.");
            }
        }
        GD.Print("");
        unexpectedFailCount.ShouldBe(0);
    }

    internal static int FindMatchingFiles(SupportedGame game, string extension, Func<SupportedGame, RszFileOption, string, string?> condition)
    {
        var count = 0;
        var matches = ReachForGodotPlugin.SelectFilesWhere(SupportedGame.Unknown, ".rcol", (game, fileOption, filepath) => {
            var result = condition(game, fileOption, filepath);
            count++;
            if (!string.IsNullOrEmpty(result)) {
                return filepath + ": " + result;
            }
            return null;
        }).ToArray();

        if (matches.Length > 0) {
            GD.Print("Found: " + matches.Length + " out of " + count);
            GD.Print("Matches: " + string.Join("\n", matches));
        }
        return matches.Length;
    }
}