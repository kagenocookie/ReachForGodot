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

            var realScene = (scene as SceneFolderProxy)?.RealFolder ?? scene;
            bool? firstChildSceneEditable = null;
            if (IsEditableToggleableScene(realScene)) {
                firstChildSceneEditable = realScene.Owner.IsEditableInstance(realScene);
            } else {
                var firstChild = realScene.AllSubfolders.Where(sub => IsEditableToggleableScene(sub)).FirstOrDefault();
                firstChildSceneEditable = firstChild?.Owner.IsEditableInstance(firstChild);
            }
            if (firstChildSceneEditable == true) {
                AddContextMenuItem("Revert editable child scenes", Callable.From((Godot.Collections.Array nodes) => ToggleScenesEditable(nodes.FirstOrDefault().As<SceneFolder>(), false)), Logo);
            } else if (firstChildSceneEditable == false) {
                AddContextMenuItem("Make child scenes editable", Callable.From((Godot.Collections.Array nodes) => ToggleScenesEditable(nodes.FirstOrDefault().As<SceneFolder>(), true)), Logo);
            }
        }
    }

    private static void ToggleScenesEditable(SceneFolder scene, bool editable)
    {
        if (IsEditableToggleableScene(scene)) {
            scene.Owner.SetEditableInstance(scene, editable);
        }
        foreach (var child in scene.Subfolders) {
            ToggleScenesEditable(child, editable);
        }
    }

    private static bool IsEditableToggleableScene(SceneFolder scene)
    {
        if (scene.Owner != null && !string.IsNullOrEmpty(scene.SceneFilePath)) {
            return true;
        }
        return false;
    }
}
#endif
