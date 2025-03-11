#if TOOLS
using System.Text.RegularExpressions;
using Godot;

namespace ReaGE;

public partial class SceneFolderContextMenuPlugin : EditorContextMenuPlugin
{
    private Texture2D? _logo;
    private Texture2D Logo => _logo ??= ResourceLoader.Load<Texture2D>("res://addons/ReachForGodot/icons/logo.png");

    public override void _PopupMenu(string[] paths)
    {
        if (paths.Length != 1) return;
        var targetNode = EditorInterface.Singleton.GetEditedSceneRoot().GetNode(paths[0]);
        if (targetNode is SceneFolder scene) {
            AddContextMenuItem("Reposition camera to center", Callable.From((Godot.Collections.Array _) => scene.EditorRepositionCamera()), Logo);
            if (scene is SceneFolderProxy || scene.Subfolders.OfType<SceneFolder>().Any()) {
                AddContextMenuItem("Toggle proxy subfolders", Callable.From((Godot.Collections.Array _) => {
                    bool? targetValue = null;
                    if (scene is SceneFolderProxy proxy) {
                        proxy.LoadAllChildren(!proxy.ShowLinkedFolder);
                    } else {
                        foreach (var ch in scene.AllSubfolders.OfType<SceneFolderProxy>()) {
                            if (targetValue == null) {
                                targetValue = !ch.ShowLinkedFolder;
                            }
                            ch.LoadAllChildren(targetValue.Value);
                        }
                    }
                }), Logo);
            }
        }
    }
}
#endif
