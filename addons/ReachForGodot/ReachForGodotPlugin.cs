using Godot;
using ReeLib;
using GC = Godot.Collections;
using System.Diagnostics;
using System.Threading.Tasks;
using ReaGE.Tools;
using ReaGE.ContentEditorIntegration;
using System.Text.Json;
using System.Text.Json.Nodes;
using ReeLib.Tools;

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
    private const string Setting_RemoteResourceSource = $"{SettingBase}/general/REE_Lib_Resource_Source";

    public static string? BlenderPath
        => EditorInterface.Singleton.GetEditorSettings().GetSetting(Setting_BlenderPathOverrides).AsString().NullIfEmpty()
            ?? EditorInterface.Singleton.GetEditorSettings().GetSetting(Setting_BlenderPath).AsString();

    public static bool IncludeMeshMaterial { get; private set; }
    public static int SceneFolderProxyThreshold { get; private set; }
    public static int UnpackerMaxThreads { get; private set; }
    public static string? ReeLibResourceSource { get; private set; }

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
    private MainWindow? mainWindowNode;

    private ResourceImportHandler importHandler = null!;


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

        // This is a bit of a hack.
        // note to self: TypeCache static constructor ends up calling OnProjectSettingsChanged, which then ends up calling RefreshToolMenu()
        // which is why we don't need to do it ourselves currently
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(TypeCache).TypeHandle);
        toolMenu.IdPressed += HandleToolMenu;
        // mainWindowNode = ResourceLoader.Load<PackedScene>("res://addons/ReachForGodot/Editor/Windows/MainWindow.tscn").Instantiate<MainWindow>();
        // EditorInterface.Singleton.GetEditorMainScreen().AddChild(mainWindowNode);
        // _MakeVisible(false);
    }

    private void OpenDonationPage()
    {
        Process.Start(new ProcessStartInfo("https://ko-fi.com/shadowcookie") { UseShellExecute = true });
    }

    public override bool _HasMainScreen()
    {
        return false;
    }

    public override void _MakeVisible(bool visible)
    {
        if (mainWindowNode == null) return;
        if (visible) {
            mainWindowNode.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            mainWindowNode.Visible = true;
        } else {
            mainWindowNode.Visible = false;
        }
    }

    public override string _GetPluginName()
    {
        return "ReachForGodot";
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
        if (id == 200) FileExtensionTools.ExtractAllFileExtensionCacheData();

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
            PathUtils.GetFileFormatFromExtension(Path.GetExtension(Path.GetFileNameWithoutExtension(current.ResourcePath.AsSpan())).Slice(1)) != KnownFileFormats.Unknown)
        ) {
            Importer.ImportResource(current.Asset!.AssetFilename, ReachForGodot.GetAssetConfig(current.Game), file);
        }
    }

    private void UpgradeResources<TResource>(string extension) where TResource : REResource, new()
    {
        foreach (var (file, current) in FindUpgradeableResources($"*.{extension}.tres", (current) => current is not TResource || current.ResourceType == KnownFileFormats.Unknown)) {
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

    internal void CheckInferrableRszData(AssetConfig config)
    {
        var sourcePath = config.Paths.ChunkPath;
        var env = config.Workspace;

        if (string.IsNullOrEmpty(config.Workspace.Config.GamePath)) {
            GD.PrintErr("Game path must be configured");
            return;
        }

        // int success, failed;
        var rszInferrer = new ResourceTools(config.Workspace) {
            BaseOutputPath = $"userdata/output/{config.Game.ToShortName()}"
        };
        rszInferrer.InferRszData();
    }

    internal static IEnumerable<(T, string, Stream)> SelectFilesWhere<T>(SupportedGame game, string extension, Func<SupportedGame, string, Stream, T?> condition) where T : class
    {
        foreach (var (curgame, filepath, stream) in FindOrExtractAllRszFilesOfType(game, extension)) {
            var success = false;
            int retryCount = 10;
            T? result = default;
            do {
                try {
                    result = condition(curgame, filepath, stream);
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
                yield return (result, filepath, stream);
            }
        }
    }

    internal static Task<(int successes, int failures)> ExecuteOnAllSourceFiles(SupportedGame game, string extension, Action<SupportedGame, string, Stream> action)
    {
        return ExecuteOnAllSourceFiles(game, extension, (a, b, c) => {
            action.Invoke(a, b, c);
            return Task.CompletedTask;
        });
    }

    internal static async Task<(int successes, int failures)> ExecuteOnAllSourceFiles(SupportedGame game, string extension, Func<SupportedGame, string, Stream, Task> action)
    {
        var count = 0;
        var countSuccess = 0;
        var fails = new List<string>();
        foreach (var (curgame, filepath, file) in FindOrExtractAllRszFilesOfType(game, extension)) {
            var success = false;
            int retryCount = 10;
            do {
                try {
                    await action(curgame, filepath, file);
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

    private static IEnumerable<(SupportedGame game, string filepath, Stream stream)> FindOrExtractAllRszFilesOfType(SupportedGame game, string extension)
    {
        SupportedGame? lastGame = null;
        foreach (var (curgame, filepath, stream) in FindOrExtractAllFilesOfType(game, extension)) {
            if (lastGame != curgame) {
                lastGame = curgame;
            }
            yield return (curgame, filepath, stream);
        }
    }

    private static IEnumerable<(SupportedGame game, string filepath, Stream stream)> FindOrExtractAllFilesOfType(SupportedGame game, string extension)
    {
        var games = game == SupportedGame.Unknown ? ReachForGodot.GameList : [game];
        foreach (var curgame in games) {
            var config = ReachForGodot.GetAssetConfig(curgame);
            if (!config.IsValid) continue;
            // check if game doesn't use the requested file extension
            if (!config.Workspace.TryGetFileExtensionVersion(extension, out _)) continue;

            foreach (var (path, strm) in config.Workspace.GetFilesWithExtension(extension)) {
                yield return (game, path, strm);
            }
        }
    }

    public override void _ExitTree()
    {
        mainWindowNode?.QueueFree();
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
        AddEditorSetting(Setting_UnpackMaxThreads, Variant.Type.Int, 8, PropertyHint.Range, "1,64");
        AddEditorSetting(Setting_BlenderPathOverrides, Variant.Type.String, string.Empty, PropertyHint.GlobalFile, "*.exe");
        AddEditorSetting(Setting_RemoteResourceSource, Variant.Type.String, "https://raw.githubusercontent.com/kagenocookie/REE-Lib-Resources/refs/heads/master/resource-info.json");
        foreach (var game in ReachForGodot.GameList) {
            AddEditorSetting(ChunkPathSetting(game), Variant.Type.String, string.Empty, PropertyHint.GlobalDir);
            AddEditorSetting(GameDirSetting(game), Variant.Type.String, string.Empty, PropertyHint.GlobalDir);
            AddEditorSetting(AdditionalPathSetting(game), Variant.Type.PackedStringArray, string.Empty, PropertyHint.GlobalDir);
            AddEditorSetting(PakFilepathSetting(game), Variant.Type.PackedStringArray, string.Empty, PropertyHint.GlobalFile, "*.pak");

            AddProjectSetting(AdditionalPathSetting(game), Variant.Type.PackedStringArray, string.Empty, PropertyHint.GlobalDir);

            // obsolete settings
            EditorInterface.Singleton.GetEditorSettings().Erase(RszPathSetting(game));
            EditorInterface.Singleton.GetEditorSettings().Erase(FilelistPathSetting(game));
            EditorInterface.Singleton.GetEditorSettings().Erase(Il2cppPathSetting(game));
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
        ReeLibResourceSource = settings.GetSetting(Setting_RemoteResourceSource).AsString()?.NullIfEmpty();
        foreach (var game in ReachForGodot.GameList) {
            var pathChunks = PathUtils.GetFilepathWithNativesFolderSuffix(settings.GetSetting(ChunkPathSetting(game)).AsString() ?? string.Empty, game);
            if (string.IsNullOrWhiteSpace(pathChunks)) {
                ReachForGodot.SetPaths(game, null);
                continue;
            }

            pathChunks = PathUtils.NormalizeSourceFolderPath(pathChunks);
            var gamedir = settings.GetSetting(GameDirSetting(game)).AsString();
            var additional = ProjectSettings.GetSetting(AdditionalPathSetting(game)).AsStringArray()
                .Concat(settings.GetSetting(AdditionalPathSetting(game)).AsStringArray())
                .Select(path => {
                    var parts = path.Split('|');
                    string? label = parts.Length >= 2 ? parts[0] : null;
                    var filepath = PathUtils.NormalizeSourceFolderPath((parts.Length >= 2 ? parts[1] : parts[0]));
                    return new LabelledPathSetting(filepath, label);
                }).ToArray();

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
            ReachForGodot.SetPaths(game, new GamePaths(game, pathChunks, gamedir, additional, paks.Distinct().ToArray()) {
                MasterConfig = masterConfig,
            });
        }
        RefreshToolMenu();
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
