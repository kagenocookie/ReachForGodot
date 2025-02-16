#if TOOLS
#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using Godot;

namespace RFG;

[GlobalClass, Tool]
public partial class AssetBrowser : Resource
{
    private AssetConfig? _assets;
    [Export] public AssetConfig? Assets
    {
        get => _assets;
        set {
            if (value != null && value != _assets && _dialog != null) {
                _dialog.CurrentPath = ReachForGodot.GetChunkPath(value.Game);
            }
            _assets = value;
        }
    }

    [ExportToolButton("Import Assets")]
    private Callable ImportAssets => Callable.From(ShowFilePicker);

    private FileDialog? _dialog;

    private void ShowFilePicker()
    {
        var config = Assets ?? AssetConfig.DefaultInstance;
        if (string.IsNullOrWhiteSpace(config.Game)) {
            GD.PrintErr($"Please select a game in the asset config file: {config.ResourcePath}");
            return;
        }

        var basepath = ReachForGodot.GetChunkPath(config.Game);
        if (basepath == null) {
            GD.PrintErr("Chunk path not configured. Set the path to the game in editor settings and select the game in the asset browser.");
            return;
        }

        if (_dialog == null) {
            _dialog = new FileDialog();
            _dialog.Filters = ["*.mesh.*", "*.tex.*", "*.scn.*"];
            _dialog.Access = FileDialog.AccessEnum.Filesystem;
            _dialog.UseNativeDialog = true;
            _dialog.FileMode = FileDialog.FileModeEnum.OpenFiles;
            _dialog.CurrentPath = basepath;
            _dialog.FileSelected += ImportAssetSync;
            _dialog.FilesSelected += ImportAssetsSync;
        }

        _dialog.Show();
    }

    private void ImportAssetSync(string filepath)
    {
        Importer.Import(filepath).Wait();
        GD.Print("File imported to " + Importer.GetDefaultImportPath(filepath));
    }

    private void ImportAssetsSync(string[] files)
    {
        if (files.Length == 0) {
            GD.PrintErr("Empty import file list");
            return;
        }

        ImportMultipleAssets(files).Wait();
        var firstImport = Importer.GetDefaultImportPath(files[0]);
        var importPaths = files.Select(f => Importer.GetDefaultImportPath(f, Assets));
        GD.Print("Files imported to:\n" + string.Join('\n', importPaths));

        if (importPaths.FirstOrDefault(x => x != null) is string str) {
            EditorInterface.Singleton.CallDeferred(EditorInterface.MethodName.OpenSceneFromPath, str);
        }
    }

    private Task ImportMultipleAssets(string[] files)
    {
        return Task.WhenAll(files.Select(file => Importer.Import(file, null, Assets)));
    }
}
#endif