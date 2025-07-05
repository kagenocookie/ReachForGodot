#if TOOLS
#nullable enable

using System.Diagnostics;
using System.Threading.Tasks;
using CustomFileBrowser;
using Godot;
using ReeLib;

namespace ReaGE;

[GlobalClass, Tool]
public partial class AssetBrowser : Resource
{
    private AssetConfig? _assets;
    [Export] public AssetConfig? Assets
    {
        get => _assets;
        set {
            var gameChanged = (value != null && (value != _assets || value.Game != _assets.Game) && _dialog != null);
            _assets = value;
            if (gameChanged) {
                ResetFilePickerPath();
            }
        }
    }

    [ExportToolButton("Import Assets")]
    private Callable ImportAssets => Callable.From(ShowNativeFilePicker);

    [ExportToolButton("Open chunk folder")]
    private Callable OpenFolder => Callable.From(() => {
        if (Assets == null || Assets.Game == SupportedGame.Unknown) {
            GD.PrintErr("Pick a game first, please");
            return;
        }
        Process.Start(new ProcessStartInfo("explorer.exe") {
            UseShellExecute = false,
            Arguments = $"\"{Assets.Paths.ChunkPath.Replace('/', '\\')}\"",
        });
    });

    private FileDialog? _dialog;

    public void ResetFilePickerPath()
    {
        if (_dialog != null && Assets != null) {
            _dialog.CurrentPath = Assets.Paths.ChunkPath;
        }
    }

    public void ShowNativeFilePicker()
    {
        Assets ??= AssetConfig.DefaultInstance;
        if (Assets.Game == SupportedGame.Unknown) {
            GD.PrintErr($"Please select a game in the asset config file: {Assets.ResourcePath}");
            return;
        }

        var basepath = Assets.Paths.ChunkPath;
        if (basepath == null) {
            GD.PrintErr("Chunk path not configured. Set the path to the game in editor settings and select the game in the asset browser.");
            return;
        }

        // ensure resource formats and stuff are registered
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(TypeCache).TypeHandle);
        if (_dialog == null) {
            _dialog = new FileDialog();
            _dialog.Filters = ["*.*"];
            _dialog.Access = FileDialog.AccessEnum.Filesystem;
            _dialog.UseNativeDialog = true;
            _dialog.FileMode = FileDialog.FileModeEnum.OpenFiles;
            _dialog.CurrentPath = basepath;
            // _dialog.FileSelected += ImportAssetSync;
            _dialog.FilesSelected += (files) => _ = ImportAssetsAsync(files, Assets);
        }

        _dialog.Show();
    }

    public void ShowFileBrowser()
    {
        if (Assets?.Workspace.ListFile == null) {
            GD.PrintErr("File list setting not defined");
            return;
        }
        // ensure resource formats and stuff are registered
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(TypeCache).TypeHandle);

        var dlg = ResourceLoader.Load<PackedScene>("res://addons/ReachForGodot/Editor/Windows/FileUnpackerUI.tscn")?.Instantiate<FileUnpackerUI>();

        dlg ??= new FileUnpackerUI();
        dlg.FileMode = FileDialog.FileModeEnum.OpenFiles;
        dlg.Game = Assets.Game;
        dlg.FilesSelected += (files) => {
            Assets = ReachForGodot.GetAssetConfig(dlg.Game);
            TriggerFileAction(files, Assets, dlg.ShouldImportFiles, dlg.FileSystem!);
        };
        ((SceneTree)(Engine.GetMainLoop())).Root.AddChild(dlg);
        dlg.SetUnparentWhenInvisible(true);
        dlg.Show();
    }

    public static void TriggerFileAction(string[] files, AssetConfig config, bool import, ICustomFileSystem fileSystem)
    {
        GD.Print($"Attempting to extract from {files.Length} paths...");
        var relativeFilepaths = files
            .SelectMany(f => !Path.GetExtension(f.AsSpan()).IsEmpty ? [f] : fileSystem.GetRecursiveFileList(f));
        if (import) {
            var tmpConfig = (AssetConfig)config.Duplicate();
            // create a new temp config with no additional paths to ensure we fetch PAK sourced files here and not get distracted by whatever other modded files we may already have
            // maybe add more action buttons to the file picker UI so we can specify Get original or Get whichever files or Find in project file system
            tmpConfig.Paths = new GamePaths(tmpConfig.Game, tmpConfig.Paths.ChunkPath, tmpConfig.Paths.Gamedir, Array.Empty<LabelledPathSetting>(), tmpConfig.Paths.PakFiles);

            var importList = relativeFilepaths
                .Select(f => PathUtils.FindSourceFilePath(PathUtils.GetFilepathWithoutNativesFolder(f), tmpConfig)!)
                .ToArray();
            _ = ImportAssetsAsync(importList, tmpConfig);
        } else {
            var success = FileUnpacker.TryExtractCustomFileList(relativeFilepaths, config);
            GD.Print("Extraction finished, success: " + success);
        }
    }

    private static async Task ImportAssetsAsync(string[] files, AssetConfig config)
    {
        if (files.Length == 0) {
            GD.PrintErr("Empty import file list");
            return;
        }

        await ImportMultipleAssets(files, config);
        var importPaths = files.Select(f => PathUtils.GetLocalizedImportPath(f, config));
        GD.Print("Files imported to:\n" + string.Join('\n', importPaths));

        var firstImport = importPaths.FirstOrDefault(x => x != null);
        if (!string.IsNullOrEmpty(firstImport) && File.Exists(ProjectSettings.GlobalizePath(firstImport))) {
            var imported = await ResourceImportHandler.ImportAsset<REResource>(firstImport).Await();
            if (imported == null) {
                GD.PrintErr("File may not have imported correctly, check in case there's any issues: " + firstImport);
            }
        }
    }

    private static Task ImportMultipleAssets(string[] files, AssetConfig config)
    {
        if (files.Length == 1) {
            return ImportSingleAsset(files[0], config, true);
        }
        return Task.WhenAll(files.Select(f => ImportSingleAsset(f, config, false)));
    }

    private static async Task ImportSingleAsset(string file, AssetConfig config, bool open)
    {
        Debug.Assert(config != null);
        if (!file.StartsWith(config.Paths.ChunkPath)) {
            config.Paths.SourcePathOverride = PathUtils.GetSourceFileBasePath(file, config);
        }
        var converter = new AssetConverter(config, GodotImportOptions.forceReimportThisStructure);
        try {
            var res = Importer.ImportResource(file, config);
            Node? resNode = null;
            if (open && res != null) {
                if (res.ResourceType.IsSceneResource()) {
                    var scene = Importer.FindOrImportAsset<PackedScene>(res.Asset!.AssetFilename, config, true);
                    if (scene != null) {
                        EditorInterface.Singleton.SelectFile(scene.ResourcePath);
                        EditorInterface.Singleton.OpenSceneFromPath(scene.ResourcePath);
                        var rootNode = EditorInterface.Singleton.GetEditedSceneRoot();
                        if (rootNode != null && rootNode.SceneFilePath == scene.ResourcePath) {
                            resNode = rootNode;
                        }
                    }
                } else {
                    EditorInterface.Singleton.SelectFile(res.ResourcePath);
                    EditorInterface.Singleton.EditResource(res);
                }
            }
            if (resNode is IImportableAsset importableNode) {
                await converter.ImportAssetAsync(importableNode, file);
            } else if (res is IImportableAsset importable) {
                await converter.ImportAssetAsync(importable, file);
                if (!string.IsNullOrEmpty(res.ResourcePath)) {
                    res.SaveOrReplaceResource(res.ResourcePath);
                }
            }
        } finally {
            config.Paths.SourcePathOverride = null;
        }
    }
}
#endif