namespace RGE;

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;

[Tool]
public partial class AsyncImporter : Node
{
    private static AsyncImporter? instance;
    private static ProgressBar? progress;

    private static CancellationTokenSource? cancellationTokenSource;

    private static AsyncImporter? node;
    private static List<ImportQueueItem> queuedImports = new();
    private static Task<Resource?>? currentImportTask;

    public override void _Process(double delta)
    {
        if (!AsyncImporter.ContinueAsyncImports()) {
            SetProcess(false);
            QueueFree();
            instance = null;
        }
    }

    public override void _EnterTree()
    {
        instance = this;
    }

    public override void _ExitTree()
    {
        instance = null;
    }

    public static void TestPopup()
    {
        EditorInterface.Singleton.PopupDialogCentered(CreateProgressPopup());
    }

    private static Popup CreateProgressPopup()
    {
        var p = ResourceLoader.Load<PackedScene>("res://addons/ReachForGodot/async_loader_popup.tscn").Instantiate<Popup>();
        progress = p.RequireChildByTypeRecursive<ProgressBar>();
        var cancelButton = p.FindChildByTypeRecursive<Button>();
        if (cancelButton != null) {
            cancelButton.Pressed += () => CancelImports();
        }
        return p;
    }

    static AsyncImporter()
    {
        System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(typeof(RszGodotConverter).Assembly)!.Unloading += (c) => {
            CancelImports();
            progress = null;
        };
    }

    public static void CancelImports()
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource = null!;
        currentImportTask = null;
        queuedImports.Clear();
        instance?.QueueFree();
        progress?.FindNodeInParents<Popup>()?.Hide();
        progress = null;
    }

    private class ImportQueueItem
    {
        public required string originalFilepath;
        public required string importFilename;
        public required SupportedGame game;
        public required Func<string, SupportedGame, Task<bool>?> importAction;
        public List<Action<Resource?>> callbacks = new();
        public Resource? resource;
        public ImportState state;
        public Task<Resource?> importTask = null!;
    }

    private enum ImportState
    {
        Pending,
        Triggered,
        Importing,
        Done,
        Failed,
    }

    public static Task<Resource?> QueueAssetImport(string originalFilepath, SupportedGame game, Action<Resource?> callback)
    {
        GD.Print("Queue asset import " + originalFilepath);
        var format = Importer.GetFileFormat(originalFilepath).format;
        switch (format) {
            case RESupportedFileFormats.Mesh:
                return QueueAssetImport(originalFilepath, game, format, Importer.ImportMesh, callback).importTask;
            default:
                return Task.FromException<Resource?>(new ArgumentException("Invalid import asset " + originalFilepath));
        }
    }

    private static ImportQueueItem QueueAssetImport(string originalFilepath, SupportedGame game, RESupportedFileFormats format, Func<string, SupportedGame, Task<bool>?> importAction, Action<Resource?> callback)
    {
        var list = queuedImports.FirstOrDefault(qi => qi.originalFilepath == originalFilepath && qi.game == game);
        if (list == null) {
            var config = ReachForGodot.GetAssetConfig(game);
            queuedImports.Add(list = new ImportQueueItem() {
                originalFilepath = originalFilepath,
                importFilename = Importer.GetAssetImportPath(originalFilepath, format, config),
                game = game,
                importAction = importAction,
            });
        }
        list.callbacks.Add(callback);
        if (node == null) {
            GD.Print("Creating importer node");
            if (instance == null) {
                var root = ((SceneTree)Engine.GetMainLoop()).Root;
                instance = new AsyncImporter() { Name = nameof(AsyncImporter) };
                root.CallDeferred(Window.MethodName.AddChild, instance);
                EditorInterface.Singleton.PopupDialogCentered(CreateProgressPopup());
                progress!.ShowPercentage = true;
                progress.MinValue = 0;
            }
            progress!.MaxValue = queuedImports.Count;
            // progress.Value = queue
        }
        cancellationTokenSource ??= new CancellationTokenSource();
        list.importTask = AwaitResource(originalFilepath, cancellationTokenSource.Token);

        return list;
    }

    private static bool ContinueAsyncImports()
    {
        var first = queuedImports.FirstOrDefault();
        if (first == null) return false;

        var importCount = queuedImports.Count;

        // TODO show popup with an asset count list
        // EditorInterface.Singleton.PopupDialogCentered();
        GD.Print("Pending imports: " + importCount + ". Current: " + first.state);

        if (first.state == ImportState.Pending) {
            currentImportTask = HandleImportAsync(first);
        }

        Debug.Assert(currentImportTask != null);

        if (currentImportTask.IsCompleted == true) {
            queuedImports.Remove(first);
            currentImportTask = null;
        }
        return true;
    }

    private static async Task<Resource?> HandleImportAsync(ImportQueueItem item)
    {
        cancellationTokenSource ??= new CancellationTokenSource();
        item.state = ImportState.Triggered;
        var convertTask = item.importAction.Invoke(item.originalFilepath, item.game);
        if (convertTask == null) {
            GD.PrintErr("Asset conversion setup failed: " + item.originalFilepath);
            ExecutePostImport(item);
            return null;
        }

        var convertSucess = await convertTask;
        if (!convertSucess)  {
            GD.PrintErr("Asset conversion failed: " + item.originalFilepath);
            ExecutePostImport(item);
            return null;
        }
        item.state = ImportState.Importing;

        var fs = EditorInterface.Singleton.GetResourceFilesystem();
        if (!fs.IsScanning()) {
            GD.Print("Triggered rescan");
            fs.CallDeferred(EditorFileSystem.MethodName.Scan);
            await Task.Delay(50, cancellationTokenSource.Token);
            GD.Print("Triggered rescan delay 1: " + fs.IsScanning());
            while (fs.IsScanning()) {
                await Task.Delay(50, cancellationTokenSource.Token);
                GD.Print("Triggered rescan more delay: " + fs.IsScanning());
            }
        }

        var res = await AwaitResource(item.importFilename, cancellationTokenSource.Token);
        item.state = ImportState.Done;
        item.resource = res;
        ExecutePostImport(item);
        return res;
    }

    private static void ExecutePostImport(ImportQueueItem item)
    {
        foreach (var cb in item.callbacks) {
            cb.Invoke(item.resource);
        }
    }

    private async static Task<Resource?> AwaitResource(string importPath, CancellationToken token)
    {
        while (!ResourceLoader.Exists(importPath)) {
            await Task.Delay(50, token);
        }
        Resource? res = null;
        var attempts = 10;
        while (attempts-- > 0) {
            try {
                res = ResourceLoader.Load<Resource>(importPath);
            } catch (Exception) {
                await Task.Delay(100, token);
            }
        }

        if (attempts >= 0) {
            GD.PrintErr("Asset import timed out: " + importPath);
        }

        return res;
    }
}