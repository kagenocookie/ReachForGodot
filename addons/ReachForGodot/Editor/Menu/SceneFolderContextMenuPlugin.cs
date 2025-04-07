#if TOOLS
using Godot;

namespace ReaGE;

public partial class SceneFolderContextMenuPlugin : EditorContextMenuPlugin
{
    private Texture2D? _logo;
    private Texture2D Logo => _logo ??= ResourceLoader.Load<Texture2D>("res://addons/ReachForGodot/icons/logo.png");

    public override void _PopupMenu(string[] paths)
    {
        if (paths.Length != 1) return;
        var root = EditorInterface.Singleton.GetEditedSceneRoot();
        if (root == null) return;

        var targetNode = root.GetNode(paths[0]);
        if (targetNode is SceneFolder scene) {
            HandleSceneFolder(scene);
            HandleGameObjectContainer(scene);
        } else if (targetNode is RequestSetCollider set) {
            HandleRcolRequestSet(set);
        } else if (targetNode is RequestSetCollisionGroup group) {
            HandleRcolRequestColliderGroup(group);
        } else if (targetNode is GameObject) {
            HandleGameObjectContainer(targetNode);
        }

        if (targetNode is SceneFolder or GameObject) {
            var game = (targetNode as SceneFolder)?.Game ?? (targetNode as GameObject)?.Game ?? SupportedGame.Unknown;
            ShowTemplateOptions(targetNode, game);
        }
    }

    private void ShowTemplateOptions(Node targetNode, SupportedGame game)
    {
        if (game == SupportedGame.Unknown) return;

        var menu = new PopupMenu();
        if (targetNode is GameObject gameObject) {
            menu.AddItem("Create new...", 10000);
        }

        var templates = ObjectTemplateManager.GetAvailableTemplates(ObjectTemplateType.GameObject, game);
        if (templates.Length == 0) return;

        int i = 0;
        foreach (var template in templates) {
            menu.AddItem(Path.GetFileNameWithoutExtension(template).Capitalize(), i++);
        }
        menu.IdPressed += (id) => HandleTemplateItems(targetNode, id, game);
        AddContextSubmenuItem("Templates", menu, Logo);
    }

    private void HandleTemplateItems(Node parent, long id, SupportedGame game)
    {
        if (id < 10000) {
            var templates = ObjectTemplateManager.GetAvailableTemplates(ObjectTemplateType.GameObject, game);
            var chosenTemplate = templates[id];

            var obj = ObjectTemplateManager.InstantiateGameobject(chosenTemplate, parent, EditorInterface.Singleton.GetEditedSceneRoot());
            obj?.ReSetupComponents();
        } else if (id == 10000) {
            // create new
            var root = new ObjectTemplateRoot();
            var dupe = ((GameObject)parent).Clone();
            root.AddChild(dupe);
            var name = dupe.Name;
            dupe.Owner = root;
            var baseOutputPath = ObjectTemplateManager.GetUserTemplateFolder(ObjectTemplateType.GameObject, game) + name;
            int idx = 0;
            var outputPath = baseOutputPath + ".tscn";
            while (ResourceLoader.Exists(outputPath)) {
                idx++;
                outputPath = baseOutputPath + idx + ".tscn";
            }
            root.Name = name;
            root.ExtractResources();
            var scene = root.SaveAsScene(outputPath);
            EditorInterface.Singleton.SelectFile(scene.ResourcePath);
        }
    }

    private void HandleSceneFolder(SceneFolder scene)
    {
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

    private void HandleGameObjectContainer(Node container)
    {
        if (container is GameObject or SceneFolder) {
            AddContextMenuItem("Find objects", Callable.From((Godot.Collections.Array _) => CustomSearchWindow.ShowGameObjectSearch(container)), Logo);
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

    private void HandleRcolRequestSet(RequestSetCollider set)
    {
        var root = set.GetParentOrNull<RcolRootNode>();
        if (root == null) return;

        AddContextMenuItem("Show exclusively this", Callable.From((Godot.Collections.Array _) => {
            root.HideGroupsExcept(set.Group);
            EditorInterface.Singleton.EditNode(set.Group);
        }), Logo);
    }

    private void HandleRcolRequestColliderGroup(RequestSetCollisionGroup group)
    {
        var root = group.FindNodeInParents<RcolRootNode>();
        if (root == null) return;

        AddContextMenuItem("Show exclusively this", Callable.From((Godot.Collections.Array _) => {
            root.HideGroupsExcept(group);
        }), Logo);
    }
}
#endif
