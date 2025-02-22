#if TOOLS
using Godot;
using Godot.Collections;

namespace RGE;

public partial class SceneFolderInspectorPlugin : EditorInspectorPlugin, ISerializationListener
{
    private static System.Collections.Generic.Dictionary<GodotObject, Control> nodes = new();

    private PackedScene? inspectorScene;

    public void OnAfterDeserialize()
    {
    }

    public void OnBeforeSerialize()
    {
        // workaround for editor assembly unload, need to yeet anything that has closure reference to objects (buttons)
        foreach (var (node, ui) in nodes) {
            if (IsInstanceValid(ui) && IsInstanceValid(node)) {
                ui.GetParent().RemoveChild(ui);
                ui.Free();
            }
        }
        nodes.Clear();
    }

    public override bool _CanHandle(GodotObject @object)
    {
        return @object is IRszContainerNode;
    }

    public override void _ParseBegin(GodotObject @object)
    {
        if (@object is IRszContainerNode rsz) {
            CreateUI(rsz);
        }
    }

    private Button CreateButton(string label, Action action)
    {
        var btn = new Button() { Text = label };
        btn.Pressed += action;
        return btn;
    }

    private void CreateUI(IRszContainerNode obj)
    {
        inspectorScene ??= ResourceLoader.Load<PackedScene>("res://addons/ReachForGodot/Editor/Inspectors/SceneFolderInspector.tscn");
        var container = inspectorScene.Instantiate<Control>();

        var uiContainer = new VBoxContainer();
        uiContainer.AddThemeConstantOverride("Separation", 4);

        if (container.GetNode<Button>("%ShowSourceFile") is Button btn) {
            if (!string.IsNullOrEmpty(obj.Asset?.AssetFilename)) {
                btn.Pressed += () => obj.Asset?.OpenSourceFile(obj.Game);
            } else {
                btn.Visible = false;
            }
        }

        if (container.GetNode<Button>("%Find3DNode") is Button btn2) {
            if (obj is SceneFolder) {
                btn2.Pressed += obj.Find3DNode;
            } else {
                btn2.Visible = false;
            }
        }

        var importType = container.GetNode<OptionButton>("%ImportTypeOption");
        importType.Clear();
        importType.AddItem("Import setting", 0);
        importType.AddItem("Placeholders only", (int)RszGodotConverter.PresetImportModes.PlaceholderImport);
        importType.AddItem("Only what's missing", (int)RszGodotConverter.PresetImportModes.ImportMissingItems);
        if (obj is SceneFolder or PrefabNode) {
            importType.AddItem("Import changes on top of current data", (int)RszGodotConverter.PresetImportModes.ImportTreeChanges);
            importType.AddItem("Fully reimport everything", (int)RszGodotConverter.PresetImportModes.FullReimport);
        }

        var importBtn = container.GetNode<Button>("%ImportButton");
        importBtn.Visible = false;
        importBtn.Pressed += () => {
            if (importType.GetSelectedId() == 0) return;
            var options = ((RszGodotConverter.PresetImportModes)importType.GetSelectedId()).ToOptions();
            if (obj is SceneFolder scn) scn.BuildTree(options);
            if (obj is PrefabNode pfb) pfb.BuildTree(options);
        };

        importType.ItemSelected += (val) => {
            importBtn.Visible = val != 0;
        };

        AddCustomControl(container);
        nodes[(GodotObject)obj] = container;
    }
}
#endif