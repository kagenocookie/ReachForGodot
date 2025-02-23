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
        return @object is IRszContainerNode;
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
            if (obj is SceneFolder) {
                btn2.Pressed += obj.Find3DNode;
            } else {
                btn2.Visible = false;
            }
        }

        var importType = container.GetNode<OptionButton>("%ImportTypeOption");
        importType.Clear();
        importType.AddItem("Placeholders only", (int)RszGodotConverter.PresetImportModes.PlaceholderImport);
        importType.AddItem("Only what's missing", (int)RszGodotConverter.PresetImportModes.ImportMissingItems);
        if (obj is SceneFolder or PrefabNode) {
            importType.AddItem("Fully import just this scene, no subfolders", (int)RszGodotConverter.PresetImportModes.ThisFolderOnly);
            importType.AddItem("Import changes on top of current data", (int)RszGodotConverter.PresetImportModes.ImportTreeChanges);
            importType.AddItem("Fully reimport everything", (int)RszGodotConverter.PresetImportModes.FullReimport);
        } else {
            importType.AddItem("Import changes on top of current data", (int)RszGodotConverter.PresetImportModes.ImportTreeChanges);
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