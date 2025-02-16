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
    private const string Setting_GameChunkPath = $"{SettingBase}/paths/{{game}}/game_chunk_path";
    private const string Setting_Il2cppPath = $"{SettingBase}/paths/{{game}}/il2cpp_dump_file";
    private const string Setting_RszJsonPath = $"{SettingBase}/paths/{{game}}/rsz_json_file";

    private static readonly Dictionary<string, GamePaths> paths = new();

    public static string BlenderPath => EditorInterface.Singleton.GetEditorSettings().GetSetting(Setting_BlenderPath).AsString()
        ?? throw new System.Exception("Blender path not defined in editor settings");

    public static GamePaths? GetPaths(string game)
    {
        if (paths.Count == 0) OnProjectSettingsChanged();
        return paths.TryGetValue(game, out var path) ? path : null;
    }

    public static string? GetChunkPath(string game) => GetPaths(game)?.ChunkPath;

    private static string ChunkPathSetting(string game) => Setting_GameChunkPath.Replace("{game}", game);
    private static string Il2cppPathSetting(string game) => Setting_Il2cppPath.Replace("{game}", game);
    private static string RszPathSetting(string game) => Setting_RszJsonPath.Replace("{game}", game);

    public override void _EnterTree()
    {
        AddSettings();

        EditorInterface.Singleton.GetEditorSettings().SettingsChanged += OnProjectSettingsChanged;
        OnProjectSettingsChanged();
    }

    private void AddSettings()
    {
        foreach (var game in GameList) {
            AddEditorSetting(ChunkPathSetting(game), Variant.Type.String, string.Empty, PropertyHint.GlobalDir);
            AddEditorSetting(Il2cppPathSetting(game), Variant.Type.String, string.Empty, PropertyHint.GlobalFile, "*.json");
            AddEditorSetting(RszPathSetting(game), Variant.Type.String, string.Empty, PropertyHint.GlobalFile, "*.json");
        }
    }

    private static void OnProjectSettingsChanged()
    {
        var settings = EditorInterface.Singleton.GetEditorSettings();
        foreach (var game in GameList) {
            var pathChunks = settings.GetSetting(ChunkPathSetting(game)).AsString() ?? string.Empty;
            var pathIl2cpp = settings.GetSetting(Il2cppPathSetting(game)).AsString();
            var pathRsz = settings.GetSetting(RszPathSetting(game)).AsString();

            if (string.IsNullOrWhiteSpace(pathChunks)) {
                paths.Remove(game);
            } else {
                pathChunks = pathChunks.Replace('\\', '/');
                if (!pathChunks.EndsWith('/')) {
                    pathChunks = pathChunks + '/';
                }

                paths[game] = new GamePaths(game, pathChunks, pathIl2cpp, pathRsz);
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
}

public record GamePaths(string Game, string ChunkPath, string? Il2cppPath, string? RszJsonPath);

#endif //TOOLS
