namespace RGE;

using System;
using System.Threading.Tasks;
using Godot;

[Tool]
public partial class AsyncImporter : Node
{
    private static AsyncImporter? instance;
    private static AsyncLoaderPopup? popup;

    private static CancellationTokenSource? cancellationTokenSource;

    private static AsyncImporter? node;
    private static Queue<ImportQueueItem> queuedImports = new();
    private static int asyncLoadCompletedTasks;

    private class AsyncLoaderPopup
    {
        public ProgressBar? progress;
        public Label? label;
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
        public Task<Resource?> awaitTask = null!;
    }

    private enum ImportState
    {
        Pending,
        Triggered,
        Importing,
        Done,
        Failed,
    }

    public override void _Process(double delta)
    {
        if (AsyncImporter.ContinueAsyncImports()) {
            if (popup == null) {
                CreateProgressPopup();
            }

            var totalCount = queuedImports.Count + asyncLoadCompletedTasks;
            var doneCount = asyncLoadCompletedTasks;

            if (popup?.progress?.IsInsideTree() == true) {
                popup.progress.ShowPercentage = true;
                popup.progress.MinValue = 0;
                popup.progress.MaxValue = totalCount;
                popup.progress.Value = doneCount;
            }

            if (popup?.label?.IsInsideTree() == true) {
                popup.label.Text = $"Converting assets ({doneCount}/{totalCount}) ...";
            }
        } else {
            SetProcess(false);
            QueueFree();
            HideProgressPopup();
            instance = null;
            asyncLoadCompletedTasks = 0;
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
        CreateProgressPopup();
    }

    private static void HideProgressPopup()
    {
        popup?.progress?.FindNodeInParents<Window>()?.Hide();
        popup = null;
    }

    private static void CreateProgressPopup()
    {
        var p = ResourceLoader.Load<PackedScene>("res://addons/ReachForGodot/async_loader_popup.tscn").Instantiate<Window>();
        popup = new AsyncLoaderPopup() {
            progress = p.RequireChildByTypeRecursive<ProgressBar>(),
            label = p.RequireChildByTypeRecursive<Label>(),
        };
        var cancelButton = p.FindChildByTypeRecursive<Button>();
        if (cancelButton != null) {
            cancelButton.Pressed += () => CancelImports();
        }
        p.SetUnparentWhenInvisible(true);
        p.PopupExclusiveCentered(((SceneTree)(Engine.GetMainLoop())).Root);
    }

    static AsyncImporter()
    {
        System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(typeof(RszGodotConverter).Assembly)!.Unloading += (c) => {
            CancelImports();
            popup = null;
        };
    }

    // public static ImportContext CreateAsyncContext()
    // {
    //     var ctx = new ImportContext();
    //     queuedImports.Enqueue(new ImportQueueItem() {
    //         game = SupportedGame.Unknown,
    //         importAction = null!,
    //         importFilename = string.Empty,
    //         originalFilepath = string.Empty,
    //         state = ImportState.Triggered,
    //         awaitTask = ctx.AwaitTasks().ContinueWith(_ => ((Resource?)null)),
    //     });
    //     EnsureImporterNode();
    //     return ctx;
    // }

    public static void CancelImports()
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource = null;
        queuedImports.Clear();
        instance?.QueueFree();
        HideProgressPopup();
    }

    public static Task<Resource?> QueueAssetImport(string originalFilepath, SupportedGame game, Action<Resource?>? callback = null)
    {
        var format = Importer.GetFileFormat(originalFilepath).format;
        switch (format) {
            case RESupportedFileFormats.Mesh:
                return QueueAssetImport(originalFilepath, game, format, Importer.ImportMesh, callback).awaitTask;
            case RESupportedFileFormats.Texture:
                return QueueAssetImport(originalFilepath, game, format, Importer.ImportTexture, callback).awaitTask;
            default:
                return Task.FromException<Resource?>(new ArgumentException("Invalid import asset " + originalFilepath));
        }
    }

    private static ImportQueueItem QueueAssetImport(string originalFilepath, SupportedGame game, RESupportedFileFormats format, Func<string, SupportedGame, Task<bool>?> importAction, Action<Resource?>? callback)
    {
        var queueItem = queuedImports.FirstOrDefault(qi => qi.originalFilepath == originalFilepath && qi.game == game);
        if (queueItem == null) {
            var config = ReachForGodot.GetAssetConfig(game);
            queuedImports.Enqueue(queueItem = new ImportQueueItem() {
                originalFilepath = originalFilepath,
                importFilename = Importer.GetAssetImportPath(originalFilepath, format, config),
                game = game,
                importAction = importAction,
            });
        }
        if (callback != null) {
            queueItem.callbacks.Add(callback);
        }
        if (node == null) {
            EnsureImporterNode();
        }
        cancellationTokenSource ??= new CancellationTokenSource();
        queueItem.awaitTask = AwaitResource(queueItem, cancellationTokenSource.Token);

        return queueItem;
    }

    private static AsyncImporter EnsureImporterNode()
    {
        if (instance == null) {
            var root = ((SceneTree)Engine.GetMainLoop()).Root;
            instance = new AsyncImporter() { Name = nameof(AsyncImporter) };
            root.CallDeferred(Window.MethodName.AddChild, instance);
        }
        return instance;
    }

    private static bool ContinueAsyncImports()
    {
        if (!queuedImports.TryPeek(out var first)) return false;

        if (first.state == ImportState.Pending) {
            first.importTask = HandleImportAsync(first);
        }
        first.importTask ??= first.awaitTask;

        if (first.importTask.IsCompleted == true || first.state == ImportState.Failed) {
            queuedImports.Dequeue();
        }
        return queuedImports.Count != 0;
    }

    private static async Task<Resource?> HandleImportAsync(ImportQueueItem item)
    {
        cancellationTokenSource ??= new CancellationTokenSource();
        item.state = ImportState.Triggered;
        var convertTask = item.importAction.Invoke(item.originalFilepath, item.game);
        if (convertTask == null) {
            ExecutePostImport(item);
            item.state = ImportState.Failed;
            return null;
        }

        var convertSucess = await convertTask;
        if (!convertSucess)  {
            GD.PrintErr("Asset conversion failed: " + item.originalFilepath);
            ExecutePostImport(item);
            item.state = ImportState.Failed;
            return null;
        }
        item.state = ImportState.Importing;

        var fs = EditorInterface.Singleton.GetResourceFilesystem();
        if (!fs.IsScanning()) {
            fs.CallDeferred(EditorFileSystem.MethodName.Scan);
            await Task.Delay(50, cancellationTokenSource.Token);
            while (fs.IsScanning()) {
                await Task.Delay(25, cancellationTokenSource.Token);
            }
            // a bit of extra delay to give the user a chance to cancel
            await Task.Delay(250, cancellationTokenSource.Token);
        }

        var res = await AwaitResource(item, cancellationTokenSource.Token);
        item.state = ImportState.Done;
        item.resource = res;
        ExecutePostImport(item);
        return res;
    }

    private static void ExecutePostImport(ImportQueueItem item)
    {
        asyncLoadCompletedTasks++;
        foreach (var cb in item.callbacks) {
            cb.Invoke(item.resource);
        }
    }

    private async static Task<Resource?> AwaitResource(ImportQueueItem queueItem, CancellationToken token)
    {
        while (!ResourceLoader.Exists(queueItem.importFilename)) {
            await Task.Delay(50, token);
            if (queueItem.state == ImportState.Failed) {
                return null;
            }
        }
        Resource? res = null;
        var attempts = 10;
        while (attempts-- > 0) {
            try {
                res = ResourceLoader.Load<Resource>(queueItem.importFilename);
            } catch (Exception) {
                await Task.Delay(100, token);
            }
        }

        if (attempts >= 0) {
            GD.PrintErr("Asset import timed out: " + queueItem.importFilename);
        }

        return res;
    }
}