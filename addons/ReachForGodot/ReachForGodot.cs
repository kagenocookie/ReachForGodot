using System.Collections.Generic;
using System.IO;
using Godot;
using GC = Godot.Collections;

#if TOOLS
namespace RFG;
#nullable enable

[Tool]
public partial class ReachForGodot : EditorPlugin
{
    public static readonly string[] GameList = ["DragonsDogma2"];
    private const string SettingBase = "reach_for_godot";
    private const string Setting_BlenderPath = "filesystem/import/blender/blender_path";
    private const string Setting_GameChunkPath = $"{SettingBase}/paths/game_chunk_paths/{{game}}";

    private static readonly Dictionary<string, string> paths = new();

    public static string BlenderPath => EditorInterface.Singleton.GetEditorSettings().GetSetting(Setting_BlenderPath).AsString()
        ?? throw new System.Exception("Blender path not defined in editor settings");

    public static string? GetChunkPath(string game)
    {
        if (paths.Count == 0) OnProjectSettingsChanged();
        return paths.TryGetValue(game, out var path) ? path : null;
    }

    private static string ChunkPathSetting(string game) => Setting_GameChunkPath.Replace("{game}", game);

    public override void _EnterTree()
    {
        base._EnterTree();

        //Add plugin settings
        AddSettings();

        EditorInterface.Singleton.GetEditorSettings().SettingsChanged += OnProjectSettingsChanged;
        OnProjectSettingsChanged();
    }

    private void AddSettings()
    {
        foreach (var game in GameList) {
            AddEditorSetting(ChunkPathSetting(game), Variant.Type.String, string.Empty);
        }
        // AddEditorSetting(Setting_BlenderPath, Variant.Type.String, "C:/Program Files/Blender Foundation/Blender 4.3/blender.exe", PropertyHint.File, "*.exe");
    }

    private static void OnProjectSettingsChanged()
    {
        foreach (var game in GameList) {
            var setting = ChunkPathSetting(game);
            var path = EditorInterface.Singleton.GetEditorSettings().GetSetting(setting).AsString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path)) {
                paths.Remove(game);
            } else {
                path = path.Replace('\\', '/');
                if (!path.EndsWith('/')) {
                    path = path + '/';
                }

                paths[game] = path;
            }
        }
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
        if (settings.HasSetting(name)) {
            return;
        }

        var dict = new GC.Dictionary();
        dict.Add("name", name);
        dict.Add("type", (int)type);
        dict.Add("hint", (int)hint);
        if (hintstring != null) {
            dict.Add("hint_string", hintstring);
        }

        settings.Set(name, initialValue);
        settings.SetInitialValue(name, initialValue, false);
        settings.AddPropertyInfo(dict);
    }
}
#endif //TOOLS
