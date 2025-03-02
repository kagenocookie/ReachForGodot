namespace RGE;

using System;
using System.Threading.Tasks;
using Godot;

[Tool]
public partial class AsyncImporter : Window
{
    private static AsyncImporter? instance;
    public static AsyncImporter? Instance => instance;

    private static CancellationTokenSource? cancellationTokenSource;

    private static AsyncImporter? node;
    private static Dictionary<string, Task<Resource?>> finishedResources = new();
    private static Queue<ImportQueueItem> queuedImports = new();
    private static List<(Task task, Action? cancel)> externalTasks = new();
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
        Imported,
        Done,
        Failed,
    }

    public string? CurrentAction { get; set; }
    public (int total, int finished) SceneCount { get; set; }
    public (int total, int finished) PrefabCount { get; set; }
    public (int total, int finished) AssetCount { get; set; }

    private double hideElapsedTime = 0;
    private const double HideDelaySeconds = 0.5;
    private int hideElapsedProcessFrames = 0;

    public override void _Process(double delta)
    {
        if (AsyncImporter.ContinueAsyncImports() || externalTasks.Any(ext => !ext.task.IsCompleted)) {
            hideElapsedTime = 0;
            hideElapsedProcessFrames = 0;
        } else if ((hideElapsedTime += delta) >= HideDelaySeconds && hideElapsedProcessFrames++ > 10) {
            SetProcess(false);
            if (cancellationTokenSource?.IsCancellationRequested == true) {
                foreach (var t in externalTasks) {
                    t.cancel?.Invoke();
                }
            }
            externalTasks.Clear();
            finishedResources.Clear();
            asyncLoadCompletedTasks = 0;
            Hide();
            instance = null;
            return;
        }
        UpdatePopup();
    }

    private void UpdatePopup()
    {
        if (!Visible) {
            Show();
        }

        var totalCount = queuedImports.Count + asyncLoadCompletedTasks;
        var doneCount = asyncLoadCompletedTasks;
        if (GetNode<Label>("%CurrentAction") is Label action) {
            action.Text = string.IsNullOrEmpty(CurrentAction) ? "Importing assets ..." : CurrentAction;
        }

        if (GetNode<Control>("%ScenesStatus") is Control status1) {
            if (SceneCount.total == 0) {
                status1.Visible = false;
            } else {
                status1.Visible = true;
                if (status1.TryFindChildByType<Label>(out var label)) label.Text = $"Scenes: {SceneCount.finished}/{SceneCount.total}";
                if (status1.TryFindChildByType<ProgressBar>(out var progress)) {
                    progress.Value = SceneCount.finished;
                    progress.MaxValue = SceneCount.total;
                }
            }
        }

        if (GetNode<Control>("%PrefabsStatus") is Control status2) {
            if (PrefabCount.total == 0) {
                status2.Visible = false;
            } else {
                status2.Visible = true;
                if (status2.TryFindChildByType<Label>(out var label)) label.Text = $"Gameobjects: {PrefabCount.finished}/{PrefabCount.total}";
                if (status2.TryFindChildByType<ProgressBar>(out var progress)) {
                    progress.Value = PrefabCount.finished;
                    progress.MaxValue = PrefabCount.total;
                }
            }
        }

        if (GetNode<Control>("%ComponentsStatus") is Control status3) {
            if (AssetCount.total == 0) {
                status3.Visible = false;
            } else {
                status3.Visible = true;
                if (status3.TryFindChildByType<Label>(out var label)) label.Text = $"Components: {AssetCount.finished}/{AssetCount.total}";
                if (status3.TryFindChildByType<ProgressBar>(out var progress)) {
                    progress.Value = AssetCount.finished;
                    progress.MaxValue = AssetCount.total;
                }
            }
        }

        if (GetNode<Control>("%OperationsStatus") is Control status4) {
            var finished = asyncLoadCompletedTasks;
            var total = asyncLoadCompletedTasks + queuedImports.Count;
            if (total == 0) {
                status4.Visible = false;
            } else {
                status4.Visible = true;
                if (status4.TryFindChildByType<Label>(out var label)) label.Text = $"Queued operations: {finished}/{total}";
                if (status4.TryFindChildByType<ProgressBar>(out var progress)) {
                    progress.Value = finished;
                    progress.MaxValue = total;
                }
            }
        }
    }

    public override void _EnterTree()
    {
        instance = this;
    }

    public override void _ExitTree()
    {
        foreach (var t in externalTasks) {
            t.cancel?.Invoke();
        }
        instance = null;
    }

    public static void TestPopup()
    {
        CreateProgressPopup();
    }

    private static AsyncImporter CreateProgressPopup()
    {
        instance = ResourceLoader.Load<PackedScene>("res://addons/ReachForGodot/Editor/async_loader_popup.tscn").Instantiate<AsyncImporter>();
        var cancelButton = instance.FindChildByTypeRecursive<Button>();
        instance.SetUnparentWhenInvisible(true);
        // instance.PopupExclusiveCentered(((SceneTree)(Engine.GetMainLoop())).Root);
        // instance.PopupCentered(((SceneTree)(Engine.GetMainLoop())).Root);
        ((SceneTree)(Engine.GetMainLoop())).Root.AddChild(instance);
        return instance;
    }

    static AsyncImporter()
    {
        System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(typeof(RszGodotConverter).Assembly)!.Unloading += (c) => {
            instance?.CancelImports();
        };
    }

    public static void StartAsyncOperation(Task task, Action onCancel)
    {
        cancellationTokenSource ??= new CancellationTokenSource();
        externalTasks.Add((task, onCancel));
        EnsureImporterNode();
    }

    private void CancelImports()
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource = null;
        queuedImports.Clear();
        instance?.Hide();
    }

    public static Task<Resource?> QueueAssetImport(string originalFilepath, SupportedGame game, Action<Resource?>? callback = null)
    {
        if (finishedResources.TryGetValue(originalFilepath, out var existingTask)) {
            return existingTask;
        }
        var format = PathUtils.GetFileFormat(originalFilepath).format;
        switch (format) {
            case RESupportedFileFormats.Mesh:
                if (!Importer.IsSupportedMeshFile(originalFilepath, game)) {
                    return Task.FromResult((Resource?)null);
                }

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
                importFilename = PathUtils.GetAssetImportPath(originalFilepath, format, config)!,
                game = game,
                importAction = importAction,
            });
            cancellationTokenSource ??= new CancellationTokenSource();
            queueItem.awaitTask = AwaitResource(queueItem, cancellationTokenSource.Token);
        }
        if (callback != null) {
            queueItem.callbacks.Add(callback);
        }
        if (node == null) {
            EnsureImporterNode();
        }

        return queueItem;
    }

    private static AsyncImporter EnsureImporterNode()
    {
        if (instance == null) {
            instance = CreateProgressPopup();
        }
        return instance;
    }

    private static bool ContinueAsyncImports()
    {
        if (!queuedImports.TryPeek(out var first)) return false;

        if (first.state == ImportState.Pending) {
            first.importTask = HandleImportAsync(first);
        }
        // first.importTask ??= first.awaitTask;
        Debug.Assert(first.importTask != null);

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

        item.state = ImportState.Imported;
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
        finishedResources[item.originalFilepath] = item.awaitTask;
    }

    private async static Task<Resource?> AwaitResource(ImportQueueItem queueItem, CancellationToken token)
    {
        while (queueItem.state <= ImportState.Importing) await Task.Delay(50, token);

        if (queueItem.state == ImportState.Failed) {
            return null;
        }

        while (!ResourceLoader.Exists(queueItem.importFilename)) {
            await Task.Delay(50, token);
            if (queueItem.state == ImportState.Failed) {
                return null;
            }
        }
        Resource? res = null;
        var attempts = 10;
        while (attempts-- > 0 && res == null) {
            try {
                res = ResourceLoader.Load<Resource>(queueItem.importFilename);
            } catch (Exception) {
                await Task.Delay(100, token);
            }
        }

        if (attempts < 0) {
            GD.PrintErr("Asset import timed out: " + queueItem.importFilename);
        }

        return res;
    }
}