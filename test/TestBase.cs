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

    protected static TResource CreateTempResource<TResource, TExported, TImported>(
        AssetConverter converter,
        ConverterBase<TResource, TExported, TImported> fileConverter,
        string fullpath
    )
        where TImported : GodotObject
        where TResource : Resource
    {
        return fileConverter.CreateOrReplaceResourcePlaceholder(new AssetReference(PathUtils.FullToRelativePath(fullpath, converter.AssetConfig)!));
    }

    protected static IEnumerable<(T, T)> PairEnumerate<T>(IEnumerable<T> var1, IEnumerable<T> var2)
    {
        var it1 = var1.GetEnumerator();
        var it2 = var2.GetEnumerator();

        while (it1.MoveNext() && it2.MoveNext()) {
            yield return (it1.Current, it2.Current);
        }
    }

    protected static async Task<TExported?> ExportToMemory<TResource, TExported, TImported>(
        RszToolConverter<TResource, TExported, TImported> converter,
        TImported resource,
        int fileVersion
    )
        where TImported : GodotObject
        where TExported : BaseFile
        where TResource : REResource
    {
        var f = converter.CreateFile(new FileHandler(new MemoryStream()) { FileVersion = fileVersion });
        return await converter.Export(resource, f) && f.Write() ? f : null;
    }

    protected static Task ExecuteFullReadTest(
        string extension,
        Action<SupportedGame, RszFileOption, string> action,
        Dictionary<SupportedGame, int>? expectedFails = null,
        params SupportedGame[] skipGames)
    {
        return ExecuteFullReadTest(extension, (g, o, f) => {
            action.Invoke(g, o, f);
            return Task.CompletedTask;
        }, expectedFails, skipGames);
    }

    protected static async Task ExecuteFullReadTest(
        string extension,
        Func<SupportedGame, RszFileOption, string, Task> action,
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
            // game doesn't use the requested file extension
            if (!PathUtils.TryGetFileExtensionVersion(game, extension, out _)) {
                continue;
            }

            var config = ReachForGodot.GetAssetConfig(game);
            if (!config.IsValid) {
                unconfigured++;
                continue;
            }

            var (successCount, failedCount) = await ReachForGodotPlugin.ExecuteOnAllSourceFiles(game, extension, action);
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