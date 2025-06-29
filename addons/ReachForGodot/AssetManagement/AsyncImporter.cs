namespace ReaGE;

using System;
using System.Threading.Tasks;
using Godot;

[Tool]
public partial class AsyncImporter : Window
{
    private static AsyncImporter? instance;
    public static AsyncImporter? Instance => instance;

    private static CancellationTokenSource? cancellationTokenSource;

    private static Dictionary<string, Task<Resource?>> finishedResources = new();
    private static Queue<ImportQueueItem> queuedImports = new();
    private static List<(Task task, Action? cancel)> externalTasks = new();
    private static int asyncLoadCompletedTasks;

    private sealed class ImportQueueItem
    {
        public required string originalFilepath;
        public required string importFilename;
        public required SupportedGame game;
        public required Func<string, string, Task<bool>?> importAction;
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
        System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(typeof(AsyncImporter).Assembly)!.Unloading += (c) => {
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
        ResourceImportHandler.CancelImports();
        queuedImports.Clear();
        instance?.Hide();
    }

    public static Task<Resource?> QueueAssetImport(string originalFilepath, string targetFilepath, SupportedGame game, Func<string, string, Task<bool>?> importAction)
    {
        var importFilepath = ProjectSettings.LocalizePath(targetFilepath);
        var queueItem = queuedImports.FirstOrDefault(qi => qi.importFilename == importFilepath);
        if (queueItem == null) {
            queuedImports.Enqueue(queueItem = new ImportQueueItem() {
                originalFilepath = originalFilepath,
                importFilename = importFilepath,
                game = game,
                importAction = importAction,
            });
            cancellationTokenSource ??= new CancellationTokenSource();
            queueItem.awaitTask = AwaitResource(queueItem, cancellationTokenSource.Token);
        }
        EnsureImporterNode();

        return queueItem.awaitTask;
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
        var convertTask = item.importAction.Invoke(item.originalFilepath, ProjectSettings.GlobalizePath(item.importFilename));
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

        item.resource = await ResourceImportHandler.ImportAsset<Resource>(item.importFilename, cancellationTokenSource.Token).Await();
        item.state = ImportState.Done;
        ExecutePostImport(item);
        return item.resource;
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

        var resourceToken = ResourceImportHandler.ImportAsset<Resource>(queueItem.importFilename, token);
        return await resourceToken.Await();
    }
}