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
    [ExportToolButton("Import Assets")]
    private Callable ImportAssets => Callable.From(ShowFilePicker);

    private FileDialog? _dialog;

    private void ShowFilePicker()
    {
        if (string.IsNullOrWhiteSpace(AssetConfig.Instance.Game)) {
            GD.PrintErr($"Please select a game in the asset config file: {AssetConfig.Instance.ResourcePath}");
            return;
        }

        var basepath = ReachForGodot.GetChunkPath(AssetConfig.Instance.Game);
        if (basepath == null) {
            GD.PrintErr("Chunk path not configured. Set the path to the game in editor settings and select the game in the asset browser.");
            return;
        }

        if (_dialog == null) {
            _dialog = new FileDialog();
            _dialog.Filters = ["*.mesh.*", "*.tex.*"];
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
        GD.Print("Files imported to:\n" + string.Join('\n', files.Select(f => Importer.GetDefaultImportPath(f))));
    }

    public static Task ImportMultipleAssets(string[] files)
    {
        return Task.WhenAll(files.Select(file => Importer.Import(file)));
    }
}
#endif