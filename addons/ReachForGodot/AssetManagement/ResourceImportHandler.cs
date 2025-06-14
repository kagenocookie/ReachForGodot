#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Godot;

namespace ReaGE;

/// <summary>
/// Takes care of making Godot import new resources, as cleanly as the API allows, and awaiting it.
/// </summary>
[GlobalClass, Tool]
public partial class ResourceImportHandler : Resource
{
    public static bool IsImporting => _isWaitingForImport || inProgressImports.Count != 0;
    private static bool _isWaitingForImport;

    private static ResourceImportHandler? _instance;
    public static ResourceImportHandler Instance => _instance ??= new ResourceImportHandler();

    private readonly Dictionary<string, ResourceImportPromise> pendingImports = new();
    private static readonly HashSet<string> inProgressImports = new();

    private static CancellationTokenSource? cancellationTokenSource;

    private static Task? _notImportingTask;
    private static Task StopImporting => _notImportingTask != null ? _notImportingTask : (_notImportingTask = AwaitNotImporting());
    private EditorFileSystem _fileSystem = null!;

    private static DateTime lastImportTime;
    private const float ImportTimeoutSeconds = 30f;

    internal void Init()
    {
        var fs = EditorInterface.Singleton.GetResourceFilesystem();
        _fileSystem = fs;
        fs.ResourcesReimporting += OnImporting;
        fs.ResourcesReimported += OnStopImporting;
    }

    internal void Cleanup()
    {
        var fs = EditorInterface.Singleton.GetResourceFilesystem();
        fs.ResourcesReimporting -= OnImporting;
        fs.ResourcesReimported -= OnStopImporting;
    }

    private void OnImporting(string[] files)
    {
        foreach (var f in files) inProgressImports.Add(f);
        _isWaitingForImport = false;
        lastImportTime = DateTime.Now;
    }

    private void OnStopImporting(string[] files)
    {
        if (inProgressImports.Count == 0) return;
        foreach (var file in files) {
            inProgressImports.Remove(file);
            var lower = file.ToLowerInvariant();
            if (pendingImports.TryGetValue(lower, out var pending)) {
                pending.resource = ResourceLoader.Load(file);
                pending.status = ResourceImportStatus.Imported;
                pendingImports.Remove(lower);
            }
        }
    }

    public static void CancelImports()
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource = null;
    }

    public static async Task<bool> EnsureImported<T>(string importFilepath) where T : Resource
    {
        if (ResourceLoader.Exists(importFilepath)) {
            return ResourceLoader.Load<T>(importFilepath) != null;
        }

        return await ImportAsset<T>(importFilepath).Await() != null;
    }

    public static ResourceOrImportPromise<T> ImportAsset<T>(string importFilepath) where T : Resource
    {
        return ImportAsset<T>(importFilepath, (cancellationTokenSource ??= new CancellationTokenSource()).Token);
    }

    public static ResourceOrImportPromise<T> ImportAsset<T>(string importFilepath, CancellationToken cancellationToken) where T : Resource
    {
        Debug.Assert(File.Exists(ProjectSettings.GlobalizePath(importFilepath)));

        var reimported = AttemptReimport(importFilepath, false);
        if (reimported) {
            return new (ResourceLoader.Load<T>(importFilepath), null);
        }

        return new (null, QueueImport(importFilepath, reimported, cancellationToken));
    }

    private static async Task AwaitNotImporting()
    {
        while (IsImporting) await Task.Delay(1);
        _notImportingTask = null;
    }

    private static ResourceImportPromise QueueImport(string importFilepath, bool hasTriggeredReimport, CancellationToken cancellationToken)
    {
        var handler = Instance;
        var exists = ResourceLoader.Exists(importFilepath);
        var lower = importFilepath.ToLowerInvariant();
        if (handler.pendingImports.TryGetValue(lower, out var token)) {
            return token;
        }

        token = new ResourceImportPromise(importFilepath, exists);
        token.ImportTask = CreateAwaitTask(token, hasTriggeredReimport, cancellationToken);
        handler.pendingImports.Add(lower, token);
        return token;
    }

    private static bool AttemptReimport(string importFilepath, bool forceReimport)
    {
        // prevent forcing a double reimport for new assets
        if (!forceReimport && ResourceLoader.Exists(importFilepath)) {
            return true;
        }

        var fs = _instance!._fileSystem;
        if (IsImporting || fs.IsScanning()) return false;
        _isWaitingForImport = true;

        if (!ResourceLoader.Exists(importFilepath)) {
            fs.UpdateFile(importFilepath);
            // there's no method that tells godot to specifically import a new non-text file for whatever reason
            // so the only reliable way is to call a full Scan()
            // UpdateFile() + ReimportFiles() doesn't do it at the moment
            fs.Scan();
        } else {
            fs.ReimportFiles([importFilepath]);
        }

        return ResourceLoader.Exists(importFilepath);
    }

    private static async Task<Resource?> CreateAwaitTask(ResourceImportPromise task, bool hasTriggeredReimport, CancellationToken cancellationToken)
    {
        var realFilepath = ProjectSettings.GlobalizePath(task.importFilepath);
        if (!File.Exists(realFilepath)) {
            do {
                await Task.Delay(50, cancellationToken);
                if (task.IsElapsedMoreThanSeconds(30)) {
                    GD.PrintErr("Asset import timed out, file does not exist: " + task.importFilepath);
                    return null;
                }
            } while (!File.Exists(realFilepath));
            task.ResetTimer();
        }

        task.status = ResourceImportStatus.Importing;
        if (!hasTriggeredReimport) {
            var pendingImportCount = _instance!.pendingImports.Count;
            // take into account the number of pending files
            // this way, if there's a lot them, we don't just abort when it might still be doing things
            while (!AttemptReimport(task.importFilepath, false)) {
                if (cancellationToken.IsCancellationRequested) return null;
                if ((lastImportTime - DateTime.Now).TotalSeconds > ImportTimeoutSeconds) {
                    GD.PrintErr("Asset import timed out, could not be imported: " + task.importFilepath);
                    task.status = ResourceImportStatus.Failed;
                    return null;
                }
                await Task.Delay(5, cancellationToken);
            }
            task.ResetTimer();
        }

        while (!ResourceLoader.Exists(task.importFilepath)) {
            if (task.IsElapsedMoreThanSeconds(30)) {
                GD.PrintErr("Asset import timed out, could not be imported: " + task.importFilepath);
                task.status = ResourceImportStatus.Failed;
            }
            if (task.status == ResourceImportStatus.Failed) return null;
            await Task.Delay(50, cancellationToken);
        }

        task.status = ResourceImportStatus.Imported;
        return ResourceLoader.Load(task.importFilepath);
    }
}

public readonly record struct ResourceOrImportPromise<T>(T? resource, ResourceImportPromise? promise) where T : Resource
{
    public async Task<T?> Await()
    {
        return resource ?? await promise!.ImportTask as T;
    }
}

public class ResourceImportPromise
{
    public DateTime importStartTime;
    public ResourceImportStatus status;
    public Resource? resource;
    public string importFilepath;
    public bool hasExistedBefore;
    public Task<Resource?> ImportTask = null!;

    public bool IsElapsedMoreThanSeconds(int seconds) => (DateTime.Now - importStartTime).TotalSeconds > seconds;
    public void ResetTimer() => importStartTime = DateTime.Now;

    public ResourceImportPromise(string importFilepath, bool hasExistedBefore)
    {
        importStartTime = DateTime.Now;
        this.importFilepath = importFilepath;
        this.hasExistedBefore = hasExistedBefore;
    }
}

public enum ResourceImportStatus
{
    Unknown,
    Importing,
    Imported,
    Failed,
}