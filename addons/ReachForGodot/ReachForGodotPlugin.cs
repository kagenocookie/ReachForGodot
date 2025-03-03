using Godot;
using GC = Godot.Collections;

#if TOOLS
namespace RGE;
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
    private const string Setting_OutputPaths = $"{SettingBase}/paths/export_output_paths";

    public static IEnumerable<ExportPathSetting> ExportPaths => exportPaths;
    public static string BlenderPath => EditorInterface.Singleton.GetEditorSettings().GetSetting(Setting_BlenderPath).AsString()
        ?? throw new System.Exception("Blender path not defined in editor settings");

    private static string ChunkPathSetting(SupportedGame game) => Setting_GameChunkPath.Replace("{game}", game.ToString());
    private static string Il2cppPathSetting(SupportedGame game) => Setting_Il2cppPath.Replace("{game}", game.ToString());
    private static string RszPathSetting(SupportedGame game) => Setting_RszJsonPath.Replace("{game}", game.ToString());

    private static readonly List<ExportPathSetting> exportPaths = new();

    private static ReachForGodotPlugin? _pluginInstance;

    private EditorInspectorPlugin[] inspectors = new EditorInspectorPlugin[5];
    private EditorNode3DGizmoPlugin[] gizmos = Array.Empty<EditorNode3DGizmoPlugin>();
    private PopupMenu toolMenu = null!;
    private AssetBrowser? browser;

    public override void _EnterTree()
    {
        _pluginInstance = this;
        AddSettings();

        toolMenu = new PopupMenu() { Title = "RE ENGINE: Import assets ..." };
        AddToolSubmenuItem(toolMenu.Title, toolMenu);

        EditorInterface.Singleton.GetEditorSettings().SettingsChanged += OnProjectSettingsChanged;
        OnProjectSettingsChanged();
        AddInspectorPlugin(inspectors[0] = new SceneFolderInspectorPlugin());
        AddInspectorPlugin(inspectors[1] = new AssetReferenceInspectorPlugin());
        AddInspectorPlugin(inspectors[2] = new ResourceInspectorPlugin());
        AddInspectorPlugin(inspectors[3] = new AssetExportInspectorPlugin());
        AddInspectorPlugin(inspectors[4] = new GameObjectInspectorPlugin());
        RefreshToolMenu();
        toolMenu.IdPressed += (id) => {
            if (id < 100) {
                var game = (SupportedGame)id;
                OpenAssetImporterWindow(game);
            }
        };
    }

    private void RefreshToolMenu()
    {
        toolMenu.Clear();
        foreach (var game in ReachForGodot.GameList) {
            var pathSetting = ChunkPathSetting(game);
            if (!string.IsNullOrEmpty(EditorInterface.Singleton.GetEditorSettings().GetSetting(pathSetting).AsString())) {
                toolMenu.AddItem($"Import {game} assets...", (int)game);
            }
        }
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
        browser.CallDeferred(AssetBrowser.MethodName.ShowFilePicker);
    }

    public override void _ExitTree()
    {
        RemoveToolMenuItem(toolMenu.Title);
        browser = null;
        foreach (var insp in inspectors) {
            RemoveInspectorPlugin(insp);
        }
    }

    private void AddSettings()
    {
        AddEditorSetting(Setting_OutputPaths, Variant.Type.PackedStringArray, string.Empty, PropertyHint.GlobalDir);
        foreach (var game in ReachForGodot.GameList) {
            AddEditorSetting(ChunkPathSetting(game), Variant.Type.String, string.Empty, PropertyHint.GlobalDir);
            AddEditorSetting(Il2cppPathSetting(game), Variant.Type.String, string.Empty, PropertyHint.GlobalFile, "*.json");
            AddEditorSetting(RszPathSetting(game), Variant.Type.String, string.Empty, PropertyHint.GlobalFile, "*.json");
        }
    }

    public static void ReloadSettings()
    {
        _pluginInstance?.OnProjectSettingsChanged();
    }

    private void OnProjectSettingsChanged()
    {
        var settings = EditorInterface.Singleton.GetEditorSettings();
        foreach (var game in ReachForGodot.GameList) {
            var pathChunks = settings.GetSetting(ChunkPathSetting(game)).AsString() ?? string.Empty;
            var pathIl2cpp = settings.GetSetting(Il2cppPathSetting(game)).AsString();
            var pathRsz = settings.GetSetting(RszPathSetting(game)).AsString();

            if (string.IsNullOrWhiteSpace(pathChunks)) {
                ReachForGodot.SetConfiguration(game, null, null);
            } else {
                pathChunks = pathChunks.Replace('\\', '/');
                if (!pathChunks.EndsWith('/')) {
                    pathChunks = pathChunks + '/';
                }

                ReachForGodot.SetConfiguration(game, null, new GamePaths(game, pathChunks, pathIl2cpp, pathRsz));
            }
        }
        exportPaths.Clear();
        foreach (var path in settings.GetSetting(Setting_OutputPaths).AsStringArray()) {
            var parts = path.Split('|');
            string? label = parts.Length >= 2 ? parts[0] : null;
            var filepath = parts.Length >= 2 ? parts[1] : parts[0];
            exportPaths.Add(new ExportPathSetting(filepath, label));
        }
        RefreshToolMenu();
    }

    private void AddProjectSetting(string name, Variant.Type type, Variant initialValue)
    {
        if (ProjectSettings.HasSetting(name)) {
            return;
        }

        var dict = new GC.Dictionary();
        dict.Add("name", name);
        dict.Add("type", (int)type);
        dict.Add("hint", (int)PropertyHint.None);

        ProjectSettings.Singleton.Set(name, initialValue);
        ProjectSettings.SetInitialValue(name, initialValue);
        ProjectSettings.AddPropertyInfo(dict);
    }

    private void AddEditorSetting(string name, Variant.Type type, Variant initialValue, PropertyHint hint = PropertyHint.None, string? hintstring = null)
    {
        var settings = EditorInterface.Singleton.GetEditorSettings();
        if (!settings.HasSetting(name)) {
            settings.Set(name, initialValue);
        }

        var dict = new GC.Dictionary();
        dict.Add("name", name);
        dict.Add("type", (int)type);
        dict.Add("hint", (int)hint);
        if (hintstring != null) {
            dict.Add("hint_string", hintstring);
        }

        settings.SetInitialValue(name, initialValue, false);
        settings.AddPropertyInfo(dict);
    }

    public void OnBeforeSerialize()
    {
        toolMenu.Clear();
    }

    public void OnAfterDeserialize()
    {
        RefreshToolMenu();
        _pluginInstance = this;
    }
}

#endif //TOOLS
