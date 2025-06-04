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
        return @object is IExportableAsset || (@object as Node)?.Owner is IExportableAsset;
    }

    public override void _ParseBegin(GodotObject @object)
    {
        if (@object is IExportableAsset res && res.Asset?.IsEmpty == false) {
            CreateUI(res);
        } else if (@object is Node rszNode && rszNode.FindRszOwner() is IRszContainer parentRes) {
            CreateUI(parentRes);
        }
        base._ParseBegin(@object);
    }

    private void CreateUI(IExportableAsset res)
    {
        var config = ReachForGodot.GetAssetConfig(res.Game);
        if (!config.IsValid) {
            AddCustomControl(new Label() { Text = $"{res.Game} is not fully configured. Please define at least a chunk path in editor settings." });
            return;
        }

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
        var paths = res is ImportedResource
            ? config.Paths.AdditionalPaths.Append(new LabelledPathSetting("...", "Browse ...")).ToArray()
            : config.Paths.AdditionalPaths.Append(new LabelledPathSetting("...", "Browse ...")).Append(new LabelledPathSetting(config.ImportBasePath, "Current project")).ToArray();
        foreach (var path in paths) {
            exportPath.AddItem(path.DisplayLabel);
            if (path.path == selected?.path) {
                selectedIndex = i;
            }
            i++;
        }
        exportPath.Selected = selectedIndex;
        void UpdateShowButton() {
            if (exportPath.Selected == -1) {
                showBtn.Visible = false;
                return;
            }
            var curpath = paths[exportPath.Selected].path;
            if (curpath == "..." && lastBrowseTargetPath != null && res.Asset != null) {
                curpath = Path.Combine(lastBrowseTargetPath, res.Asset.GetFilenameWithExtensions(config));
            }
            showBtn.Visible = (File.Exists(PathUtils.ResolveExportPath(curpath, res.Asset!, res.Game)));
        }
        exportPath.ItemSelected += (id) => {
            ReachForGodot.LastExportPath = paths[id];
            button.Disabled = id == -1;
            UpdateShowButton();
        };
        button.Disabled = selectedIndex == -1;
        var commitExport = (string chosenBasePath) => {
            if (res is ImportedResource importedres) {
                var outputPath = PathUtils.ResolveExportPath(chosenBasePath, res.Asset, res.Game)
                    ?? throw new Exception("Could not resolve export path");
                Directory.CreateDirectory(outputPath.GetBaseDir());
                File.Copy(ProjectSettings.GlobalizePath(importedres.ResourcePath), outputPath, true);
                GD.Print("File copied to " + outputPath);
                UpdateShowButton();
                return;
            }

            if (res?.Asset?.IsEmpty != false) {
                GD.PrintErr("Asset path not defined");
                return;
            }

            var conv = new AssetConverter(res.Game, GodotImportOptions.placeholderImport);
            var task = conv.ExportAsset(res, chosenBasePath);
            task.Wait();
            var success = task.Result;
            if (success) {
                GD.Print("Export successful!");
                UpdateShowButton();
            } else {
                GD.PrintErr("Export failed");
            }
        };
        button.Pressed += () => {
            var selectedPath = paths[exportPath.Selected];
            if (selectedPath.path == "...") {
                var defaultPath = (lastBrowseTargetPath == null ? null : Path.Combine(lastBrowseTargetPath, res.Asset?.GetFilenameWithExtensions(config) ?? ""))
                    ?? PathUtils.ResolveExportPath(paths[0], res.Asset?.AssetFilename, res.Game)
                    ?? paths[0].path;
                var dlg = new FileDialog() { FileMode = FileDialog.FileModeEnum.SaveFile, Access = FileDialog.AccessEnum.Filesystem, UseNativeDialog = true };
                dlg.CurrentPath = defaultPath;
                dlg.FileSelected += (selectedFn) => {
                    lastBrowseTargetPath = selectedFn.GetBaseDir();
                    commitExport(selectedFn);
                };
                dlg.Popup();
                return;
            }
            commitExport(selectedPath);
        };
        UpdateShowButton();
        showBtn.Pressed += () => {
            var curpath = paths[exportPath.Selected].path;
            if (curpath == "..." && lastBrowseTargetPath != null && res.Asset != null) {
                curpath = Path.Combine(lastBrowseTargetPath, res.Asset.GetFilenameWithExtensions(config));
            }
            FileSystemUtils.ShowFileInExplorer(PathUtils.ResolveExportPath(curpath, res.Asset, res.Game));
        };

        AddCustomControl(container);
    }

    private static string? lastBrowseTargetPath;
}
#endif