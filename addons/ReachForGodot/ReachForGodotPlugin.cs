using System.Threading.Tasks;
using Godot;
using RszTool;
using GC = Godot.Collections;
using System.Diagnostics;
using System.Threading.Tasks;


#if ENABLE_TESTS
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
    private const string Setting_GameChunkPath = $"{SettingBase}/paths/{{game}}/game_chunk_path";
    private const string Setting_Il2cppPath = $"{SettingBase}/paths/{{game}}/il2cpp_dump_file";
    private const string Setting_RszJsonPath = $"{SettingBase}/paths/{{game}}/rsz_json_file";
    private const string Setting_FilelistPath = $"{SettingBase}/paths/{{game}}/file_list";
    private const string Setting_PakFilepath = $"{SettingBase}/paths/{{game}}/pak_priority_list";
    private const string Setting_AdditionalPaths = $"{SettingBase}/paths/{{game}}/additional_paths";

    private const string Setting_ImportMeshMaterials = $"{SettingBase}/general/import_mesh_materials";
    private const string Setting_SceneFolderProxyThreshold = $"{SettingBase}/general/create_scene_proxy_node_threshold";
    private const string Setting_FileUnpackerExe = $"{SettingBase}/general/file_unpacker_executable";

    public static string? BlenderPath => EditorInterface.Singleton.GetEditorSettings().GetSetting(Setting_BlenderPath).AsString();

    public static bool IncludeMeshMaterial { get; private set; }
    public static int SceneFolderProxyThreshold { get; private set; }
    public static string? UnpackerExeFilepath { get; private set; }

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
    private EditorImportPlugin[] importers = Array.Empty<EditorImportPlugin>();

    private const string DonateButtonText = "Reach for Godot: Donate on Ko-fi";
    private PopupMenu toolMenu = null!;
    private PopupMenu toolMenuDev = null!;
    private AssetBrowser? browser;

    static ReachForGodotPlugin()
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(TypeCache).TypeHandle);
    }

#region Backup code
    // TODO: revisit after https://github.com/godotengine/godot/issues/104937 is fixed
    public static bool IsImporting { get; private set; }
    // public static List<string> PostImportReimportfiles = new();
    // public static List<string> PostImportUpdateFiles = new();
    // public static readonly Queue<Action> ExecuteOnMainThreadList = new();

    private void OnImporting(string[] files)
    {
        IsImporting = true;
    }
    private void OnStopImporting(string[] files)
    {
        IsImporting = false;
        // TODO: revisit after https://github.com/godotengine/godot/issues/104937 is fixed
        // if (PostImportReimportfiles.Count > 0) {
        //     var fs = EditorInterface.Singleton.GetResourceFilesystem();
        //     var list = PostImportReimportfiles.ToArray();
        //     var updateFiles = PostImportUpdateFiles.ToArray();

        //     GD.Print("Post-import reimporting:\n" + string.Join("\n", list));
        //     PostImportReimportfiles.Clear();
        //     PostImportUpdateFiles.Clear();
        //     // fs.CallDeferred(EditorFileSystem.MethodName.ReimportFiles, list);
        //     // fs.ReimportFiles(list);
        //     foreach (var f in updateFiles) {
        //         // fs.UpdateFile(f);
        //         fs.CallDeferred(EditorFileSystem.MethodName.UpdateFile, f);
        //     }
        // }
    }

    // public override void _Process(double delta)
    // {
    //     while (ExecuteOnMainThreadList.Count != 0) {
    //         var next = ExecuteOnMainThreadList.Dequeue();
    //         try {
    //             next.Invoke();
    //         } catch (Exception e) {
    //             GD.PrintErr("Main thread exec failed: " + e.Message);
    //         }
    //     }
    // }
#endregion

    public override void _EnterTree()
    {
        _pluginInstance = this;
        AddSettings();
        var fs = EditorInterface.Singleton.GetResourceFilesystem();
        fs.ResourcesReimporting += OnImporting;
        fs.ResourcesReimported += OnStopImporting;

        toolMenu = new PopupMenu() { Title = "RE ENGINE Assets" };
        toolMenuDev = new PopupMenu() { Title = "Reach for Godot Dev" };
        AddToolSubmenuItem(toolMenuDev.Title, toolMenuDev);
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
        ];
        foreach (var i in inspectors) AddInspectorPlugin(i);

        contextMenus = new EditorContextMenuPlugin[1];
        AddContextMenuPlugin(EditorContextMenuPlugin.ContextMenuSlot.SceneTree, contextMenus[0] = new SceneFolderContextMenuPlugin());

        gizmos = [new MeshGizmo()];
        foreach (var gizmo in gizmos) AddNode3DGizmoPlugin(gizmo);

        // sceneImporters = new EditorSceneFormatImporter[] { };
        foreach (var i in sceneImporters) AddSceneFormatImporterPlugin(i);

        // importers = new EditorImportPlugin[] { };
        foreach (var i in importers) AddImportPlugin(i);

        RefreshToolMenu();
        toolMenu.IdPressed += HandleToolMenu;
        toolMenuDev.IdPressed += HandleToolMenu;
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
        toolMenuDev.Clear();
        toolMenu.AddItem($"Open packed file browser...", 99);
        foreach (var game in ReachForGodot.GameList) {
            var pathSetting = ChunkPathSetting(game);
            if (!string.IsNullOrEmpty(EditorInterface.Singleton.GetEditorSettings().GetSetting(pathSetting).AsString())) {
                toolMenu.AddItem($"Import {game} assets...", (int)game);
            }
        }

        toolMenu.AddItem("Upgrade imported resources", 100);

        toolMenuDev.AddItem("Extract file format versions from file lists", 200);
#if ENABLE_TESTS
        var tests = GoTest.Adapter.CreateProvider().GetTestSuites(System.Reflection.Assembly.GetExecutingAssembly());
        int testId = 1000;
        foreach (var test in tests) {
            toolMenuDev.AddItem("Test: " + (test.Name.StartsWith("Test") ? test.Name.Substring(4) : test.Name), testId++);
        }
#endif
    }

    private void HandleToolMenu(long id)
    {
        if (id < 99) {
            var game = (SupportedGame)id;
            OpenAssetImporterWindow(game);
        }
        if (id == 99) {
            OpenPackedAssetBrowser(ReachForGodot.AssetConfigs.First(c => c.IsValid));
        }
        if (id == 100) UpgradeObsoleteResources("mdf2");
        if (id == 200) ExtractFileVersions();

#if ENABLE_TESTS
        if (id >= 1000) {
            var tests = GoTest.Adapter.CreateProvider().GetTestSuites(System.Reflection.Assembly.GetExecutingAssembly());
            var test = tests[(int)(id - 1000)];
            RunTests(test.Name);
        }
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

    public void OpenPackedAssetBrowser(AssetConfig config)
    {
        browser ??= new AssetBrowser();
        browser.Assets = config;
        browser.CallDeferred(AssetBrowser.MethodName.ShowFileBrowser);
    }

    private void UpgradeObsoleteResources(string extension)
    {
        foreach (var (file, current) in FindUpgradeableResources($"*.*.tres",
            (current) => current.GetType() == typeof(REResource) &&
            PathUtils.GetFileFormatFromExtension(Path.GetExtension(Path.GetFileNameWithoutExtension(current.ResourcePath.AsSpan())).Slice(1)) != SupportedFileFormats.Unknown)
        ) {
            Importer.Import(current.Asset!.AssetFilename, ReachForGodot.GetAssetConfig(current.Game), file);
        }
    }

    private void UpgradeResources<TResource>(string extension) where TResource : REResource, new()
    {
        foreach (var (file, current) in FindUpgradeableResources($"*.{extension}.tres", (current) => current is not TResource || current.ResourceType == SupportedFileFormats.Unknown)) {
            Importer.Import(current.Asset!.AssetFilename, ReachForGodot.GetAssetConfig(current.Game), file);
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

#if ENABLE_TESTS
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

        GD.Print($"Expecting {scnTotal} scn files");
        var conv = new AssetConverter(GodotImportOptions.testImport);

        var (success, failed) = await ExecuteOnAllSourceFiles(config.Game, "scn", async (game, fileOption, filepath) => {
            using var file = new ScnFile(fileOption, new FileHandler(filepath));
            file.Read();
        });
        GD.Print($"Finished {success + failed} scn out of expected {scnTotal}");

        GD.Print($"Expecting {pfbTotal} pfb files");
        (success, failed) = await ExecuteOnAllSourceFiles(config.Game, "pfb", async (game, fileOption, filepath) => {
            using var file = new PfbFile(fileOption, new FileHandler(filepath));
            file.Read();
            file.SetupGameObjects();
            // the import process will try and autodetect any pfb propertyIds for GameObjectRef fields
            conv.Game = game;
            var node = new PrefabNode() { Asset = new AssetReference(PathUtils.FullToRelativePath(filepath, conv.AssetConfig)!) };
            await conv.Pfb.Import(file, node);
            conv.Pfb.Clear();
            node.QueueFree();
        });
        GD.Print($"Finished {success + failed} pfb out of expected {pfbTotal}");

        GD.Print($"Expecting {rcolTotal} rcol files");
        (success, failed) = await ExecuteOnAllSourceFiles(config.Game, "rcol", async (game, fileOption, filepath) => {
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

            var hasAttemptedFullExtract = false;
            var extWithVersion = PathUtils.AppendFileVersion(extension, config);
            foreach (var relativeFilepath in PathUtils.GetFilesByExtensionFromListFile(config.Paths.FilelistPath, extWithVersion, null)) {
                if (PathUtils.IsIgnoredFilepath(relativeFilepath, config)) continue;

                var resolvedFile = PathUtils.FindSourceFilePath(PathUtils.GetFilepathWithoutNativesFolder(relativeFilepath), config, false);

                if (!hasAttemptedFullExtract && string.IsNullOrEmpty(resolvedFile)) {
                    hasAttemptedFullExtract = true;
                    GD.Print($"Failed to resolve {relativeFilepath}. Attempting unpack of all {curgame} {extension} files if configured");
                    FileUnpacker.TryExtractFilteredFiles($"\\.{extWithVersion}(?:\\.[^\\/]*)?$", config);

                    if (PathUtils.FindMissingFiles(extension, config).Any()) {
                        Directory.CreateDirectory(ReachForGodot.GetUserdataPath(""));
                        var missingFilelistPath = ReachForGodot.GetUserdataPath(config.Paths.ShortName + "_missing_files.list");
                        File.WriteAllLines(missingFilelistPath, PathUtils.FindMissingFiles(extension, config));
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
        RemoveToolMenuItem(toolMenu.Title);
        RemoveToolMenuItem(toolMenuDev.Title);
        RemoveToolMenuItem(DonateButtonText);
        browser = null;
        foreach (var insp in inspectors) RemoveInspectorPlugin(insp);
        foreach (var menu in contextMenus) RemoveContextMenuPlugin(menu);
        foreach (var gizmo in gizmos) RemoveNode3DGizmoPlugin(gizmo);
        foreach (var importer in sceneImporters) RemoveSceneFormatImporterPlugin(importer);
        foreach (var importer in importers) RemoveImportPlugin(importer);

        var fs = EditorInterface.Singleton.GetResourceFilesystem();
        fs.ResourcesReimporting -= OnImporting;
        fs.ResourcesReimported -= OnStopImporting;
    }

    private void AddSettings()
    {
        AddEditorSetting(Setting_ImportMeshMaterials, Variant.Type.Bool, false);
        AddEditorSetting(Setting_SceneFolderProxyThreshold, Variant.Type.Int, 500, PropertyHint.Range, "50,5000,or_greater,hide_slider");
        AddEditorSetting(Setting_FileUnpackerExe, Variant.Type.String, string.Empty, PropertyHint.GlobalFile, "*.exe");
        foreach (var game in ReachForGodot.GameList) {
            AddEditorSetting(ChunkPathSetting(game), Variant.Type.String, string.Empty, PropertyHint.GlobalDir);
            AddEditorSetting(Il2cppPathSetting(game), Variant.Type.String, string.Empty, PropertyHint.GlobalFile, "*.json");
            AddEditorSetting(RszPathSetting(game), Variant.Type.String, string.Empty, PropertyHint.GlobalFile, "*.json");
            AddEditorSetting(FilelistPathSetting(game), Variant.Type.String, string.Empty, PropertyHint.GlobalFile);
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
        UnpackerExeFilepath = settings.GetSetting(Setting_FileUnpackerExe).AsString();
        if (UnpackerExeFilepath == "") UnpackerExeFilepath = null;
        foreach (var game in ReachForGodot.GameList) {
            var pathChunks = settings.GetSetting(ChunkPathSetting(game)).AsString() ?? string.Empty;
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
            var paks = settings.GetSetting(PakFilepathSetting(game)).AsStringArray();

            if (string.IsNullOrWhiteSpace(pathChunks)) {
                ReachForGodot.SetConfiguration(game, null, null);
            } else {
                pathChunks = PathUtils.NormalizeSourceFolderPath(pathChunks);
                ReachForGodot.SetConfiguration(game, null, new GamePaths(game, pathChunks, pathIl2cpp, pathRsz, pathFilelist, additional, paks));
            }
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
    }

    public void OnAfterDeserialize()
    {
        RefreshToolMenu();
        _pluginInstance = this;
        OnProjectSettingsChanged();
    }
}

#endif //TOOLS
