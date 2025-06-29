using Godot;
using RszTool;
using GC = Godot.Collections;
using System.Diagnostics;
using System.Threading.Tasks;
using ReaGE.Tools;
using ReaGE.ContentEditorIntegration;
using System.Text.Json;
using System.Text.Json.Nodes;

#if REAGE_DEV
using Chickensoft.GoDotTest;
#endif

#if TOOLS
namespace ReaGE;
#nullable enable

[Tool]
public partial class ReachForGodotPlugin : EditorPlugin, ISerializationListener
{
    public static ReachForGodotPlugin Instance => _pluginInstance!;

    private const string SettingBase = "reach_for_godot";

    private const string Setting_BlenderPath = "filesystem/import/blender/blender_path";
    private const string Setting_GameDir = $"{SettingBase}/paths/{{game}}/game_path";
    private const string Setting_GameChunkPath = $"{SettingBase}/paths/{{game}}/game_chunk_path";
    private const string Setting_Il2cppPath = $"{SettingBase}/paths/{{game}}/il2cpp_dump_file";
    private const string Setting_RszJsonPath = $"{SettingBase}/paths/{{game}}/rsz_json_file";
    private const string Setting_FilelistPath = $"{SettingBase}/paths/{{game}}/file_list";
    private const string Setting_PakFilepath = $"{SettingBase}/paths/{{game}}/pak_priority_list";
    private const string Setting_AdditionalPaths = $"{SettingBase}/paths/{{game}}/additional_paths";

    private const string Setting_BlenderPathOverrides = $"{SettingBase}/general/blender_override_path";
    private const string Setting_ImportMeshMaterials = $"{SettingBase}/general/import_mesh_materials";
    private const string Setting_SceneFolderProxyThreshold = $"{SettingBase}/general/create_scene_proxy_node_threshold";
    private const string Setting_UnpackMaxThreads = $"{SettingBase}/general/unpack_max_threads";

    public static string? BlenderPath
        => EditorInterface.Singleton.GetEditorSettings().GetSetting(Setting_BlenderPathOverrides).AsString().NullIfEmpty()
            ?? EditorInterface.Singleton.GetEditorSettings().GetSetting(Setting_BlenderPath).AsString();

    public static bool IncludeMeshMaterial { get; private set; }
    public static int SceneFolderProxyThreshold { get; private set; }
    public static int UnpackerMaxThreads { get; private set; }

    private static string GameDirSetting(SupportedGame game) => Setting_GameDir.Replace("{game}", game.ToString());
    private static string ChunkPathSetting(SupportedGame game) => Setting_GameChunkPath.Replace("{game}", game.ToString());
    private static string Il2cppPathSetting(SupportedGame game) => Setting_Il2cppPath.Replace("{game}", game.ToString());
    private static string RszPathSetting(SupportedGame game) => Setting_RszJsonPath.Replace("{game}", game.ToString());
    private static string AdditionalPathSetting(SupportedGame game) => Setting_AdditionalPaths.Replace("{game}", game.ToString());
    private static string FilelistPathSetting(SupportedGame game) => Setting_FilelistPath.Replace("{game}", game.ToString());
    private static string PakFilepathSetting(SupportedGame game) => Setting_PakFilepath.Replace("{game}", game.ToString());

    private static ReachForGodotPlugin? _pluginInstance;

    private EditorInspectorPlugin[] inspectors = null!;
    private EditorContextMenuPlugin[] contextMenus = null!;
    private EditorNode3DGizmoPlugin[] gizmos = Array.Empty<EditorNode3DGizmoPlugin>();
    private EditorSceneFormatImporter[] sceneImporters = Array.Empty<EditorSceneFormatImporter>();
    private EditorImportPlugin[] importers = null!;
    private EditorResourcePreviewGenerator[] previewGenerators = null!;

    private const string DonateButtonText = "Reach for Godot: Donate on Ko-fi";
    private PopupMenu toolMenu = null!;
    private PopupMenu? toolMenuDev;
    private AssetBrowser? browser;

    private ResourceImportHandler importHandler = null!;

    static ReachForGodotPlugin()
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(TypeCache).TypeHandle);
    }


    public override void _EnterTree()
    {
        _pluginInstance = this;
        AddSettings();
        importHandler = ResourceImportHandler.Instance;
        importHandler.Init();

#if REAGE_DEV
        toolMenuDev = new PopupMenu() { Title = "Reach for Godot Dev" };
        AddToolSubmenuItem(toolMenuDev.Title, toolMenuDev);
        toolMenuDev.IdPressed += HandleToolMenu;
#endif

        toolMenu = new PopupMenu() { Title = "RE ENGINE Assets" };
        AddToolSubmenuItem(toolMenu.Title, toolMenu);
        AddToolMenuItem(DonateButtonText, Callable.From(OpenDonationPage));

        EditorInterface.Singleton.GetEditorSettings().SettingsChanged += OnProjectSettingsChanged;
        ProjectSettings.SettingsChanged += OnProjectSettingsChanged;
        OnProjectSettingsChanged();

        inspectors = [
            new REObjectInspectorPlugin(),
            new SceneFolderInspectorPlugin(),
            new AssetReferenceInspectorPlugin(),
            new AssetImportInspectorPlugin(),
            new AssetExportInspectorPlugin(),
            new GameObjectInspectorPlugin(),
            new EfxInspectorPlugin(),
        ];
        foreach (var i in inspectors) AddInspectorPlugin(i);

        contextMenus = new EditorContextMenuPlugin[3];
        AddContextMenuPlugin(EditorContextMenuPlugin.ContextMenuSlot.SceneTree, contextMenus[0] = new SceneFolderContextMenuPlugin());
        AddContextMenuPlugin(EditorContextMenuPlugin.ContextMenuSlot.FilesystemCreate, contextMenus[1] = new ReaGECreateMenuPlugin());
        AddContextMenuPlugin(EditorContextMenuPlugin.ContextMenuSlot.Filesystem, contextMenus[2] = new ForceRescanMenuPlugin());

        gizmos = [
            new MeshGizmo(),
            new AIMapGizmo()
        ];
        foreach (var gizmo in gizmos) AddNode3DGizmoPlugin(gizmo);

        // sceneImporters = new EditorSceneFormatImporter[] { };
        foreach (var i in sceneImporters) AddSceneFormatImporterPlugin(i);

        previewGenerators = new [] { new ReaGEPreviewGenerator() };
        foreach (var gen in previewGenerators) EditorInterface.Singleton.GetResourcePreviewer().AddPreviewGenerator(gen);

        importers = new EditorImportPlugin[] { new GameResourceImporter() };
        foreach (var i in importers) AddImportPlugin(i);

        RefreshToolMenu();
        toolMenu.IdPressed += HandleToolMenu;
    }

    private void OpenDonationPage()
    {
        Process.Start(new ProcessStartInfo("https://ko-fi.com/shadowcookie") { UseShellExecute = true });
    }

    public override void _ShortcutInput(InputEvent @event)
    {
        base._ShortcutInput(@event);
        if (@event is InputEventKey keyEvent && keyEvent.GetModifiersMask() == KeyModifierMask.MaskCtrl && keyEvent.Keycode == Key.Period) {
            var edited = EditorInterface.Singleton.GetEditedSceneRoot();
            if (edited != null) {
                CustomSearchWindow.ShowGameObjectSearch(edited);
            }
        }
    }

    private void RefreshToolMenu()
    {
        toolMenu.Clear();
        if (ReachForGodot.ConfiguredGames.Count() != ReachForGodot.PathSetupGames.Count()) {
            toolMenu.AddItem("Create missing asset import configs", 101);
        }
        toolMenu.AddItem($"Open packed file browser...", 99);
        foreach (var game in ReachForGodot.GameList) {
            var pathSetting = ChunkPathSetting(game);
            if (!string.IsNullOrEmpty(EditorInterface.Singleton.GetEditorSettings().GetSetting(pathSetting).AsString())) {
                toolMenu.AddItem($"Import {game} assets...", (int)game);
            }
        }

        toolMenu.AddItem("Upgrade imported resources", 100);

#if REAGE_DEV
        toolMenuDev?.Clear();
        toolMenuDev?.AddItem("Extract file format versions from file lists", 200);
        var tests = GoTest.Adapter.CreateProvider().GetTestSuites(System.Reflection.Assembly.GetExecutingAssembly());
        int testId = 1000;
        var args = System.Environment.GetCommandLineArgs();
        // if (args.Contains("--test"))
        var autoTriggerTest = args.Contains("--editor-test") ? args[Array.IndexOf(args, "--editor-test") + 1] : null;
        foreach (var test in tests) {
            toolMenuDev?.AddItem("Test: " + (test.Name.StartsWith("Test") ? test.Name.Substring(4) : test.Name), testId++);
            if (!_testsRun && autoTriggerTest != null && autoTriggerTest == test.Name) {
                _testsRun = true;
                RunTests(test.Name);
                break;
            }
        }
        // if (!args.Contains("--headless")) {
        //     ReaGE.Tests.DevTools.WriteEfxStructsJson();
        // }
        toolMenuDev?.AddItem("Re-generate EFX JSON files", 301);
        // toolMenuDev?.AddItem("Extract Content Editor enum display labels", 300);
#endif
    }
#if REAGE_DEV
    private static bool _testsRun;
#endif

    private void HandleToolMenu(long id)
    {
        if (id < 99) {
            var game = (SupportedGame)id;
            OpenAssetImporterWindow(game);
        }
        if (id == 99) {
            OpenPackedAssetBrowser();
        }
        if (id == 100) UpgradeObsoleteResources("mdf2");
        if (id == 101) {
            foreach (var game in ReachForGodot.PathSetupGames) ReachForGodot.GetAssetConfig(game);
            RefreshToolMenu();
        }
        if (id == 200) ExtractFileVersions();

        if (id == 300) {
            foreach (var cfg in ReachForGodot.AssetConfigs.Where(c => c.IsValid)) {
                if (cfg.Paths.Gamedir == null) continue;
                var ce = new ContentEditor(cfg, cfg.Paths.Gamedir);
                ce.ParseDumpedEnumDisplayLabels();
            }
        }
#if REAGE_DEV
        if (id >= 1000) {
            var tests = GoTest.Adapter.CreateProvider().GetTestSuites(System.Reflection.Assembly.GetExecutingAssembly());
            var test = tests[(int)(id - 1000)];
            RunTests(test.Name);
        }

        if (id == 301) ReaGE.Tests.DevTools.WriteEfxStructsJson();
#endif
    }

    public void OpenAssetImporterWindow(SupportedGame game)
    {
        var config = ReachForGodot.GetAssetConfig(game);
        if (config == null) {
            GD.PrintErr("Could not resolve asset config for game " + game);
            return;
        }

        OpenAssetImporterWindow(config);
    }

    public void OpenAssetImporterWindow(AssetConfig config)
    {
        browser ??= new AssetBrowser();
        browser.Assets = config;
        browser.CallDeferred(AssetBrowser.MethodName.ShowNativeFilePicker);
    }

    public void OpenPackedAssetBrowser(AssetConfig? config = null)
    {
        browser ??= new AssetBrowser();
        browser.Assets = config ?? browser.Assets ?? ReachForGodot.AssetConfigs.First(c => c.IsValid);
        browser.CallDeferred(AssetBrowser.MethodName.ShowFileBrowser);
    }

    private void UpgradeObsoleteResources(string extension)
    {
        foreach (var (file, current) in FindUpgradeableResources($"*.*.tres",
            (current) => current.GetType() == typeof(REResource) &&
            PathUtils.GetFileFormatFromExtension(Path.GetExtension(Path.GetFileNameWithoutExtension(current.ResourcePath.AsSpan())).Slice(1)) != SupportedFileFormats.Unknown)
        ) {
            Importer.ImportResource(current.Asset!.AssetFilename, ReachForGodot.GetAssetConfig(current.Game), file);
        }
    }

    private void UpgradeResources<TResource>(string extension) where TResource : REResource, new()
    {
        foreach (var (file, current) in FindUpgradeableResources($"*.{extension}.tres", (current) => current is not TResource || current.ResourceType == SupportedFileFormats.Unknown)) {
            Importer.ImportResource(current.Asset!.AssetFilename, ReachForGodot.GetAssetConfig(current.Game), file);
        }
    }

    private IEnumerable<(string file, REResource current)> FindUpgradeableResources(string searchPattern, Func<REResource, bool> upgradeCondition)
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(TypeCache).TypeHandle);
        var list = Directory.EnumerateFiles(ProjectSettings.GlobalizePath("res://"), searchPattern, SearchOption.AllDirectories);
        foreach (var file in list) {
            if (file.Contains(".godot")) continue;

            var localized = ProjectSettings.LocalizePath(file);
            var current = ResourceLoader.Load<REResource>(localized);
            if (current?.Asset == null || !upgradeCondition(current)) {
                continue;
            }

            GD.Print(localized);
            yield return (file, current);
        }
        GD.Print("You may need to reopen any scenes referencing the upgraded resources");
    }

#if REAGE_DEV
    private static void RunTests(string filter)
    {
        var env = new TestEnvironment(true, false, false, false, false, filter, Array.Empty<string>());
        var tmp = new Node();
        GoTest.RunTests(System.Reflection.Assembly.GetExecutingAssembly(), tmp, env).ContinueWith(t => {
            GD.Print(t.IsCompletedSuccessfully ? "Tests finished" : "Tests failed");
            tmp.QueueFree();
        });
    }
#endif

    internal async Task FetchInferrableRszData(AssetConfig config)
    {
        var fileOption = TypeCache.CreateRszFileOptions(config);
        var sourcePath = config.Paths.ChunkPath;

        GD.Print("Checking for duplicate field names...");
        TypeCache.VerifyDuplicateFields(fileOption.RszParser.ClassDict.Values, config);

        var scnTotal = PathUtils.GetFilesByExtensionFromListFile(config.Paths.FilelistPath, PathUtils.AppendFileVersion(".scn", config), sourcePath).Count();
        var pfbTotal = PathUtils.GetFilesByExtensionFromListFile(config.Paths.FilelistPath, PathUtils.AppendFileVersion(".pfb", config), sourcePath).Count();
        var rcolTotal = PathUtils.GetFilesByExtensionFromListFile(config.Paths.FilelistPath, PathUtils.AppendFileVersion(".rcol", config), sourcePath).Count();

        var resourceFinder = new ResourceFieldFinder();

        int success, failed;

        GD.Print($"Expecting {scnTotal} scn files");
        (success, failed) = await ExecuteOnAllSourceFiles(config.Game, "scn", (game, fileOption, filepath) => {
            using var file = new ScnFile(fileOption, new FileHandler(filepath));
            file.Read();
            ReaGETools.FindDuplicateRszObjectInstances(game, file.RSZ, filepath);
            resourceFinder.CheckInstances(game, file.RSZ, file.ResourceInfoList);
        });
        GD.Print($"Finished {success + failed} scn out of expected {scnTotal}");

        GD.Print($"Expecting {pfbTotal} pfb files");
        (success, failed) = await ExecuteOnAllSourceFiles(config.Game, "pfb", (game, fileOption, filepath) => {
            using var file = new PfbFile(fileOption, new FileHandler(filepath));
            file.Read();
            file.SetupGameObjects();
            resourceFinder.CheckInstances(game, file.RSZ, file.ResourceInfoList);
            ReaGETools.FindDuplicateRszObjectInstances(game, file.RSZ, filepath);
            GameObjectRefResolver.CheckInstances(game, file);
        });
        GD.Print($"Finished {success + failed} pfb out of expected {pfbTotal}");
        resourceFinder.ApplyRszPatches();
        resourceFinder.DumpFindings(config.Paths.ShortName);

        GD.Print($"Expecting {rcolTotal} rcol files");
        (success, failed) = await ExecuteOnAllSourceFiles(config.Game, "rcol", (game, fileOption, filepath) => {
            using var file = new RcolFile(fileOption, new FileHandler(filepath));
            file.Read();
        });
        GD.Print($"Finished {success + failed} rcol out of expected {rcolTotal}");

        TypeCache.StoreInferredRszTypes(fileOption.RszParser.ClassDict.Values, config);
    }

    internal static IEnumerable<(T, string)> SelectFilesWhere<T>(SupportedGame game, string extension, Func<SupportedGame, RszFileOption, string, T?> condition) where T : class
    {
        foreach (var (curgame, fileOption, filepath) in FindOrExtractAllRszFilesOfType(game, extension)) {
            var success = false;
            int retryCount = 10;
            T? result = default;
            do {
                try {
                    result = condition(curgame, fileOption, filepath);
                    success = true;
                } catch (RszRetryOpenException) {
                    retryCount--;
                } catch (Exception e) {
                    GD.Print("Failed to read file " + filepath, e);
                    success = false;
                    break;
                }
            } while (!success && retryCount > 0);
            if (success && result != null) {
                yield return (result, filepath);
            }
        }
    }

    internal static Task<(int successes, int failures)> ExecuteOnAllSourceFiles(SupportedGame game, string extension, Action<SupportedGame, RszFileOption, string> action)
    {
        return ExecuteOnAllSourceFiles(game, extension, (a, b, c) => {
            action.Invoke(a, b, c);
            return Task.CompletedTask;
        });
    }

    internal static async Task<(int successes, int failures)> ExecuteOnAllSourceFiles(SupportedGame game, string extension, Func<SupportedGame, RszFileOption, string, Task> action)
    {
        var count = 0;
        var countSuccess = 0;
        var fails = new List<string>();
        foreach (var (curgame, fileOption, filepath) in FindOrExtractAllRszFilesOfType(game, extension)) {
            var success = false;
            int retryCount = 10;
            do {
                try {
                    await action(curgame, fileOption, filepath);
                    success = true;
                } catch (RszRetryOpenException) {
                    retryCount--;
                } catch (Exception e) {
                    GD.Print("Failed to read file " + filepath, e);
                    success = false;
                    break;
                }
            } while (!success && retryCount > 0);
            count++;
            if (success) {
                countSuccess++;
            } else {
                fails.Add(filepath);
            }
            if (count % 100 == 0) {
                GD.Print($"Handled {count} files...");
            }
        }
        GD.Print($"{game} test finished ({extension}): {countSuccess}/{count} files were successful, {fails.Count} failed");
        return (countSuccess, fails.Count);
    }

    private static IEnumerable<(SupportedGame game, RszFileOption fileOption, string filepath)> FindOrExtractAllRszFilesOfType(SupportedGame game, string extension)
    {
        SupportedGame? lastGame = null;
        RszFileOption? fileOption = null;
        foreach (var (curgame, filepath) in FindOrExtractAllFilesOfType(game, extension)) {
            if (lastGame != curgame) {
                lastGame = curgame;
                fileOption = TypeCache.CreateRszFileOptions(ReachForGodot.GetAssetConfig(curgame));
            }
            yield return (curgame, fileOption!, filepath);
        }
    }

    private static IEnumerable<(SupportedGame game, string filepath)> FindOrExtractAllFilesOfType(SupportedGame game, string extension)
    {
        var games = game == SupportedGame.Unknown ? ReachForGodot.GameList : [game];
        foreach (var curgame in games) {
            var config = ReachForGodot.GetAssetConfig(curgame);
            if (!config.IsValid) continue;
            // game doesn't use the requested file extension
            if (!PathUtils.TryGetFileExtensionVersion(game, extension, out _)) continue;

            var hasAttemptedFullExtract = false;
            var extWithVersion = PathUtils.AppendFileVersion(extension, config);
            foreach (var relativeFilepath in PathUtils.GetFilesByExtensionFromListFile(config.Paths.FilelistPath, extWithVersion, null)) {
                if (PathUtils.IsIgnoredFilepath(relativeFilepath, config)) continue;

                var resolvedFile = PathUtils.FindSourceFilePath(PathUtils.GetFilepathWithoutNativesFolder(relativeFilepath), config, false);

                if (!hasAttemptedFullExtract && string.IsNullOrEmpty(resolvedFile)) {
                    hasAttemptedFullExtract = true;
                    GD.Print($"Failed to resolve {relativeFilepath}. Attempting unpack of all {curgame} {extension} files if configured");
                    var missing = new List<string>();
                    FileUnpacker.TryExtractFilteredFiles($"\\.{extWithVersion}(?:\\.[^\\/]*)?$", config, missing);

                    if (missing.Count > 0) {
                        Directory.CreateDirectory(ProjectSettings.GlobalizePath(ReachForGodot.GetUserdataBasePath("")));
                        var missingFilelistPath = ProjectSettings.GlobalizePath(ReachForGodot.GetUserdataBasePath(config.Paths.ShortName + "_missing_files.list"));
                        File.WriteAllLines(missingFilelistPath, missing.Select(m => PathUtils.GetFilepathWithoutNativesFolder(m)));
                        GD.PrintErr("List of missing files has been written to " + missingFilelistPath);
                    }

                    resolvedFile = PathUtils.FindSourceFilePath(PathUtils.GetFilepathWithoutNativesFolder(relativeFilepath), config, false);
                }
                if (!string.IsNullOrEmpty(resolvedFile))
                    yield return (curgame, resolvedFile);
            }
        }
    }

    private static void ExtractFileVersions()
    {
        foreach (var game in ReachForGodot.GameList) {
            var filelist = EditorInterface.Singleton.GetEditorSettings().GetSetting(FilelistPathSetting(game)).AsString();
            if (!string.IsNullOrEmpty(filelist)) {
                GD.Print($"Extracting extensions for {game} from {filelist} ...");
                PathUtils.ExtractFileVersionsFromList(game, filelist);
            }
        }
    }

    public override void _ExitTree()
    {
        importHandler.Cleanup();
        RemoveToolMenuItem(toolMenu.Title);
        if (toolMenuDev != null) RemoveToolMenuItem(toolMenuDev.Title);
        RemoveToolMenuItem(DonateButtonText);
        browser = null;
        foreach (var insp in inspectors) RemoveInspectorPlugin(insp);
        foreach (var menu in contextMenus) RemoveContextMenuPlugin(menu);
        foreach (var gizmo in gizmos) RemoveNode3DGizmoPlugin(gizmo);
        foreach (var importer in sceneImporters) RemoveSceneFormatImporterPlugin(importer);
        foreach (var importer in importers) RemoveImportPlugin(importer);
        foreach (var gen in previewGenerators) EditorInterface.Singleton.GetResourcePreviewer().RemovePreviewGenerator(gen);
    }

    private void AddSettings()
    {
        AddEditorSetting(Setting_ImportMeshMaterials, Variant.Type.Bool, true);
        AddEditorSetting(Setting_SceneFolderProxyThreshold, Variant.Type.Int, 500, PropertyHint.Range, "50,5000,or_greater,hide_slider");
        AddEditorSetting(Setting_UnpackMaxThreads, Variant.Type.Int, 16, PropertyHint.Range, "1,64");
        AddEditorSetting(Setting_BlenderPathOverrides, Variant.Type.String, string.Empty, PropertyHint.GlobalFile);
        foreach (var game in ReachForGodot.GameList) {
            AddEditorSetting(ChunkPathSetting(game), Variant.Type.String, string.Empty, PropertyHint.GlobalDir);
            AddEditorSetting(Il2cppPathSetting(game), Variant.Type.String, string.Empty, PropertyHint.GlobalFile, "*.json");
            AddEditorSetting(RszPathSetting(game), Variant.Type.String, string.Empty, PropertyHint.GlobalFile, "*.json");
            AddEditorSetting(FilelistPathSetting(game), Variant.Type.String, string.Empty, PropertyHint.GlobalFile);
            AddEditorSetting(GameDirSetting(game), Variant.Type.String, string.Empty, PropertyHint.GlobalDir);
            AddEditorSetting(AdditionalPathSetting(game), Variant.Type.PackedStringArray, string.Empty, PropertyHint.GlobalDir);
            AddEditorSetting(PakFilepathSetting(game), Variant.Type.PackedStringArray, string.Empty, PropertyHint.GlobalFile, "*.pak");

            AddProjectSetting(AdditionalPathSetting(game), Variant.Type.PackedStringArray, string.Empty, PropertyHint.GlobalDir);
        }
    }

    public static void ReloadSettings()
    {
        _pluginInstance?.OnProjectSettingsChanged();
    }

    private void OnProjectSettingsChanged()
    {
        var settings = EditorInterface.Singleton.GetEditorSettings();
        IncludeMeshMaterial = settings.GetSetting(Setting_ImportMeshMaterials).AsBool();
        SceneFolderProxyThreshold = settings.GetSetting(Setting_SceneFolderProxyThreshold).AsInt32();
        UnpackerMaxThreads = settings.GetSetting(Setting_UnpackMaxThreads).AsInt32();
        foreach (var game in ReachForGodot.GameList) {
            var pathChunks = PathUtils.GetFilepathWithNativesFolderSuffix(settings.GetSetting(ChunkPathSetting(game)).AsString() ?? string.Empty, game);
            if (string.IsNullOrWhiteSpace(pathChunks)) {
                ReachForGodot.SetPaths(game, null);
                continue;
            }

            pathChunks = PathUtils.NormalizeSourceFolderPath(pathChunks);
            var gamedir = settings.GetSetting(GameDirSetting(game)).AsString();
            var pathIl2cpp = settings.GetSetting(Il2cppPathSetting(game)).AsString();
            var pathRsz = settings.GetSetting(RszPathSetting(game)).AsString();
            var pathFilelist = settings.GetSetting(FilelistPathSetting(game)).AsString();
            var additional = ProjectSettings.GetSetting(AdditionalPathSetting(game)).AsStringArray()
                .Concat(settings.GetSetting(AdditionalPathSetting(game)).AsStringArray())
                .Select(path => {
                    var parts = path.Split('|');
                    string? label = parts.Length >= 2 ? parts[0] : null;
                    var filepath = PathUtils.NormalizeSourceFolderPath((parts.Length >= 2 ? parts[1] : parts[0]));
                    return new LabelledPathSetting(filepath, label);
                }).ToArray();

            VerifyRszSource(ref pathRsz, game);

            var paks = settings.GetSetting(PakFilepathSetting(game)).AsStringArray().Select(path => PathUtils.NormalizeFilePath(path)).ToList();
            var masterConfigPath = GamePaths.GetMasterConfigFilepath(game);
            GameMasterConfig? masterConfig = null;
            if (File.Exists(masterConfigPath)) {
                using var fs = File.OpenRead(masterConfigPath);
                masterConfig = JsonSerializer.Deserialize<GameMasterConfig>(fs);
            }
            if (string.IsNullOrEmpty(gamedir)) {
                gamedir = PathUtils.NormalizeFilePath(paks.FirstOrDefault()?.GetBaseDir());
            } else {
                if (masterConfig?.PakList != null) {
                    var prepends = new List<string>();
                    foreach (var pak in masterConfig.PakList) {
                        var path = Path.Combine(gamedir, pak);
                        if (File.Exists(path)) {
                            prepends.Add(PathUtils.NormalizeFilePath(path));
                        }
                    }
                    paks.InsertRange(0, prepends);
                } else {
                    paks.InsertRange(0, PakUtils.ScanPakFiles(gamedir));
                }
            }
            ReachForGodot.SetPaths(game, new GamePaths(game, pathChunks, gamedir, pathIl2cpp, pathRsz, pathFilelist, additional, paks.Distinct().ToArray()) {
                MasterConfig = masterConfig,
            });
        }
        RefreshToolMenu();
    }

    private static void VerifyRszSource(ref string pathRsz, SupportedGame game)
    {
        if (!pathRsz.Contains("reasy", StringComparison.OrdinalIgnoreCase) || !File.Exists(pathRsz)) {
            return;
        }

        var cleanRszPath = ProjectSettings.GlobalizePath(ReachForGodot.GetUserdataPath("rsz.json", game));
        if (File.Exists(cleanRszPath) && new FileInfo(cleanRszPath).LastWriteTimeUtc > new FileInfo(pathRsz).LastWriteTimeUtc) {
            pathRsz = cleanRszPath;
            return;
        }

        GD.Print("Detected REasy sourced rsz dump json file. Attempting to clean up for ReachForGodot use...");
        using var fs = File.OpenRead(pathRsz);
        var jsondoc = JsonSerializer.Deserialize<JsonObject>(fs);
        if (jsondoc == null) {
            GD.PrintErr("Failed to deserialize file");
            return;
        }
        if (jsondoc.ContainsKey("metadata")) {
            jsondoc.Remove("metadata");
        }
        foreach (var prop in jsondoc) {
            var item = prop.Value!.AsObject();
            if (!item.ContainsKey("fields")) continue;

            var name = item["name"]!.GetValue<string>();
            if (name == "via.physics.SphereShape") {
                // our tooling expects 1 shape == 1 object, otherwise things get complicated and messy
                var sphereField = item["fields"]!.AsArray().Where(f => f!["name"]!.GetValue<string>() == "Center").FirstOrDefault();
                var radiusField = item["fields"]!.AsArray().Where(f => f!["name"]!.GetValue<string>() == "Radius").FirstOrDefault();
                if (radiusField != null) {
                    item["fields"]!.AsArray().Remove(radiusField);
                }
                if (sphereField != null) {
                    if (sphereField["size"]!.GetValue<int>() == 12) {
                        sphereField["name"] = "Sphere";
                        sphereField["size"] = 16;
                        sphereField["type"] = "Vec4";
                        sphereField["original_type"] = "via.vec4";
                    }
                }
            }

            // reasy defines some custom types that we don't care about, remap them to default types
            foreach (var field in item["fields"]!.AsArray()) {
                var type = field!["type"]!.GetValue<string>();
                if (type == "MlShape" || type == "RawBytes") {
                    field["type"] = "Data";
                } else if (type == "Bits") {
                    var size = field["size"]!.GetValue<int>();
                    if (size == 4) {
                        field["type"] = "U32";
                    } else {
                        throw new Exception("Unhandled Bits type size");
                    }
                } else if (type == "Float") {
                    field["type"] = "F32";
                }
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(cleanRszPath)!);
        using var outfs = File.OpenWrite(cleanRszPath);
        JsonSerializer.Serialize(outfs, jsondoc);
        GD.Print("Saved cleaned RSZ template to " + cleanRszPath);
        pathRsz = cleanRszPath;
    }

    private void AddProjectSetting(string name, Variant.Type type, Variant initialValue, PropertyHint hint = PropertyHint.None, string? hintstring = null)
    {
        if (!ProjectSettings.HasSetting(name)) {
            ProjectSettings.SetSetting(name, initialValue);
        }

        var dict = CreateSettingDict(name, type, hint, hintstring);

        ProjectSettings.SetInitialValue(name, initialValue);
        ProjectSettings.AddPropertyInfo(dict);
    }

    private void AddEditorSetting(string name, Variant.Type type, Variant initialValue, PropertyHint hint = PropertyHint.None, string? hintstring = null)
    {
        var settings = EditorInterface.Singleton.GetEditorSettings();
        if (!settings.HasSetting(name)) {
            settings.Set(name, initialValue);
        }

        var dict = CreateSettingDict(name, type, hint, hintstring);

        settings.SetInitialValue(name, initialValue, false);
        settings.AddPropertyInfo(dict);
    }

    private static GC.Dictionary CreateSettingDict(string name, Variant.Type type, PropertyHint hint, string? hintstring)
    {
        var dict = new GC.Dictionary();
        dict.Add("name", name);
        dict.Add("type", (int)type);
        dict.Add("hint", (int)hint);
        if (hintstring != null) {
            dict.Add("hint_string", hintstring);
        }

        return dict;
    }

    public void OnBeforeSerialize()
    {
        toolMenu.Clear();
        toolMenuDev?.Clear();
    }

    public void OnAfterDeserialize()
    {
        RefreshToolMenu();
        _pluginInstance = this;
        OnProjectSettingsChanged();
    }
}

#endif //TOOLS
