namespace ReaGE;

using System.Diagnostics;
using System.Threading.Tasks;
using Godot;

public abstract class BlenderResourceConverter<TResource, TAsset> : ConverterBase<TResource, TResource, TAsset>
    where TResource : REResourceProxy
    where TAsset : GodotObject
{
    private static CancellationTokenSource? cancellationTokenSource;
    private const int blenderTimeoutMs = 30000;

    public override TAsset? GetResourceImportedObject(TResource resource) => resource.ImportedResource as TAsset;

    private static bool _hasShownNoBlenderWarning = false;
    private static readonly string EmptyBlend = ProjectSettings.GlobalizePath("res://addons/ReachForGodot/.gdignore/empty.blend");

    protected virtual bool IsSupportedFile(string sourceFilePath)
    {
        return true;
    }

    protected abstract Task<bool> ExecuteImport(string sourceFilePath, string outputPath);

    public async Task<bool> ImportAsset(TResource resource, string? sourceFilePath = null)
    {
        return await ImportAssetGetResource(resource, sourceFilePath) != null;
    }

    public async Task<Resource?> ImportAssetGetResource(TResource resource, string? sourceFilePath = null)
    {
        sourceFilePath ??= resource.Asset?.FindSourceFile(Config);
        if (!Path.IsPathRooted(sourceFilePath)) {
            sourceFilePath = PathUtils.FindSourceFilePath(sourceFilePath, Config);
        }
        var importFilepath = PathUtils.GetAssetImportPath(sourceFilePath, resource.ResourceType, Config);

        if (sourceFilePath == null || !IsSupportedFile(sourceFilePath)) {
            GD.PrintErr("Unsupported file " + sourceFilePath);
            return null;
        }

        var outputPath = PathUtils.NormalizeFilePath(ProjectSettings.GlobalizePath(importFilepath));

        Directory.CreateDirectory(Path.GetFullPath(outputPath.GetBaseDir()));
        var assetExisted = File.Exists(outputPath) && ResourceLoader.Exists(importFilepath);
        var updatedResource = await AsyncImporter.QueueAssetImport(sourceFilePath, outputPath, Game, ExecuteImport);
        if (!string.IsNullOrEmpty(updatedResource?.ResourcePath)) {
            resource.ImportedResource = ResourceLoader.Load<Resource>(updatedResource.ResourcePath);
        }
        if (!string.IsNullOrEmpty(resource.ResourcePath)) {
            ResourceSaver.Save(resource);
            EditorInterface.Singleton.GetResourceFilesystem().UpdateFile(resource.ResourcePath);
        }
        if (assetExisted) {
            ReimportExistingFile(outputPath);
        } else {
            ForceEditorImportNewFiles();
        }
        return resource.ImportedResource;
    }

    protected static void QueueFileRescan()
    {
        var fs = EditorInterface.Singleton.GetResourceFilesystem();
        if (!fs.IsScanning()) fs.CallDeferred(EditorFileSystem.MethodName.Scan);
    }

    protected static void ForceEditorImportNewFiles()
    {
        QueueFileRescan();
    }

    protected static void ReimportExistingFile(string file)
    {
        // var fs = EditorInterface.Singleton.GetResourceFilesystem();
        // fs.CallDeferred(EditorFileSystem.MethodName.UpdateFile, file);
        // fs.CallDeferred(EditorFileSystem.MethodName.ReimportFiles, new Godot.Collections.Array<string>(new[] { file }));
        QueueFileRescan();
    }

    protected async Task ExecuteBlenderScript(string script, bool background)
    {
        var blenderPath = ReachForGodot.BlenderPath;
        if (string.IsNullOrEmpty(blenderPath)) {
            if (!_hasShownNoBlenderWarning) {
                GD.PrintErr("Blender is not configured. Meshes and textures will not import.");
                _hasShownNoBlenderWarning = true;
            }
            return;
        }

        var process = Process.Start(new ProcessStartInfo() {
            UseShellExecute = false,
            FileName = blenderPath,
            WindowStyle = background ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal,
            Arguments = background
                ? $"\"{EmptyBlend}\" --background --python-expr \"{script}\""
                : $"\"{EmptyBlend}\" --python-expr \"{script}\"",
        });

        if (cancellationTokenSource == null || cancellationTokenSource.IsCancellationRequested) {
            cancellationTokenSource = new();
        }

        var delay = Task.Delay(blenderTimeoutMs, cancellationTokenSource.Token);
        var completedTask = await Task.WhenAny(process!.WaitForExitAsync(cancellationTokenSource.Token), delay);
        if (completedTask == delay) {
            cancellationTokenSource.Cancel();
            cancellationTokenSource = null;
        }
    }
}
