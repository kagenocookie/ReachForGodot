#if TOOLS
using Godot;

namespace ReaGE;

public partial class AssetExportInspectorPlugin : EditorInspectorPlugin, ISerializationListener
{
    private static PluginSerializationFixer serializationFixer = new();

    public void OnAfterDeserialize() { }
    public void OnBeforeSerialize() => serializationFixer.OnBeforeSerialize();

    private PackedScene? inspectorScene;

    public override bool _CanHandle(GodotObject @object)
    {
        return @object is IExportableAsset;
    }

    public override void _ParseBegin(GodotObject @object)
    {
        if (@object is IExportableAsset res) {
            CreateUI(res);
        }
        base._ParseBegin(@object);
    }

    private void CreateUI(IExportableAsset res)
    {
        inspectorScene ??= ResourceLoader.Load<PackedScene>("res://addons/ReachForGodot/Editor/Inspectors/AssetExportInspectorPlugin.tscn");
        var container = inspectorScene.Instantiate<Control>();
        serializationFixer.Register((GodotObject)res, container);

        var exportPath = container.GetNode<OptionButton>("%ExportPathOption");
        var button = container.GetNode<Button>("%ExportButton");
        var showBtn = container.GetNode<Button>("%ShowExportedButton");

        var selected = ReachForGodot.LastExportPath;
        var selectedIndex = -1;
        int i = 0;
        exportPath.Clear();
        var paths = ReachForGodot.GetAssetConfig(res.Game).Paths.AdditionalPaths;
        foreach (var path in paths) {
            exportPath.AddItem(path.DisplayLabel);
            if (path.path == selected?.path) {
                selectedIndex = i;
            }
            i++;
        }
        exportPath.Selected = selectedIndex;
        void UpdateShowButton() {
            showBtn.Visible = exportPath.Selected != -1 && File.Exists(Exporter.ResolveExportPath(paths[exportPath.Selected], res.Asset!, res.Game));
        }
        exportPath.ItemSelected += (id) => {
            ReachForGodot.LastExportPath = paths[id];
            button.Disabled = id == -1;
            UpdateShowButton();
        };
        button.Disabled = selectedIndex == -1;
        button.Pressed += () => {
            if (res.Asset?.IsEmpty != false) {
                GD.PrintErr("Asset path not defined");
                return;
            }

            var success = Exporter.Export(res, paths[exportPath.Selected]);
            if (success) {
                GD.Print("Export successful!");
                UpdateShowButton();
            } else {
                GD.PrintErr("Export failed");
            }
        };
        UpdateShowButton();
        showBtn.Pressed += () => FileSystemUtils.ShowFileInExplorer(Exporter.ResolveExportPath(paths[exportPath.Selected], res.Asset, res.Game));

        // FileSystemUtils.ShowFileInExplorer(file);

        AddCustomControl(container);
    }
}
#endif