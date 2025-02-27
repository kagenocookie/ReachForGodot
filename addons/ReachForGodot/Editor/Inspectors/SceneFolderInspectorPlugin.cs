#if TOOLS
using Godot;

namespace RGE;

public partial class SceneFolderInspectorPlugin : EditorInspectorPlugin, ISerializationListener
{
    private static PluginSerializationFixer pluginSerializationFixer = new();

    public void OnAfterDeserialize() { }
    public void OnBeforeSerialize() => pluginSerializationFixer.OnBeforeSerialize();

    private PackedScene? inspectorScene;

    public override bool _CanHandle(GodotObject @object)
    {
        return @object is SceneFolder or PrefabNode;
    }

    public override void _ParseBegin(GodotObject @object)
    {
        if (@object is IRszContainerNode rsz) {
            CreateUI(rsz);
        }
    }

    private void CreateUI(IRszContainerNode obj)
    {
        inspectorScene ??= ResourceLoader.Load<PackedScene>("res://addons/ReachForGodot/Editor/Inspectors/SceneFolderInspector.tscn");
        var container = inspectorScene.Instantiate<Control>();

        if (container.GetNode<Button>("%Find3DNode") is Button btn2) {
            if (obj is SceneFolder folder) {
                btn2.Pressed += folder.EditorRepositionCamera;
            } else {
                btn2.Visible = false;
            }
        }
        if (container.GetNode<Button>("%LoadFoldersBtn") is Button loadbtn) {
            if (obj is SceneFolder scene) {
                loadbtn.Pressed += () => {
                    bool? targetValue = null;
                    if (scene is SceneFolderProxy proxy) {
                        proxy.LoadAllChildren(!proxy.Enabled);
                    } else {
                        foreach (var ch in scene.AllSubfolders.OfType<SceneFolderProxy>()) {
                            if (targetValue == null) {
                                targetValue = !ch.Enabled;
                            }
                            ch.LoadAllChildren(targetValue.Value);
                        }
                    }
                };
                var tempbtn = new Button() { Text = "Recalc bounds" };
                loadbtn.GetParent().AddChild(tempbtn);
                tempbtn.GetParent().MoveChild(tempbtn, loadbtn.GetIndex() + 1);
                tempbtn.Pressed += () => {
                    scene.RecalculateBounds(true);
                    EditorInterface.Singleton.MarkSceneAsUnsaved();
                };
            }
        }

        var importType = container.GetNode<OptionButton>("%ImportTypeOption");
        importType.Clear();
        if (obj is PrefabNode pfb) {
            importType.AddItem("Full import", (int)RszGodotConverter.PresetImportModes.ImportTreeChanges);
            importType.AddItem("Fully reimport all resources", (int)RszGodotConverter.PresetImportModes.FullReimport);
        } else if (obj is SceneFolder scn) {
            importType.AddItem("Placeholders only", (int)RszGodotConverter.PresetImportModes.PlaceholderImport);
            importType.AddItem("Import just this scene, no subfolders", (int)RszGodotConverter.PresetImportModes.ThisFolderOnly);
            importType.AddItem("Import everything", (int)RszGodotConverter.PresetImportModes.ImportMissingItems);
            importType.AddItem("Force reimport all resources", (int)RszGodotConverter.PresetImportModes.FullReimport);
        } else {
            importType.AddItem("Full import", (int)RszGodotConverter.PresetImportModes.ImportTreeChanges);
        }

        var importBtn = container.GetNode<Button>("%ImportButton");
        importBtn.Pressed += () => {
            var options = ((RszGodotConverter.PresetImportModes)importType.GetSelectedId()).ToOptions();
            if (obj is SceneFolder scn) scn.BuildTree(options);
            if (obj is PrefabNode pfb) pfb.BuildTree(options);
        };

        AddCustomControl(container);
        pluginSerializationFixer.Register((GodotObject)obj, container);
    }
}
#endif