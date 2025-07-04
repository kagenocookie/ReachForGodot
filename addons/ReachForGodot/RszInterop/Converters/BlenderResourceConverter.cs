namespace ReaGE;

using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Godot;

public abstract class BlenderResourceConverter<TResource, TAsset> : ConverterBase<TResource, TResource, TAsset>
    where TResource : REResourceProxy
    where TAsset : GodotObject
{
    private static CancellationTokenSource? cancellationTokenSource;
    private const int blenderTimeoutMs = 30000;

    public override TAsset? GetImportedAssetFromResource(TResource resource) => resource.ImportedResource as TAsset;

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

        if (sourceFilePath == null || !IsSupportedFile(sourceFilePath) || importFilepath == null) {
            GD.PrintErr("Unsupported file " + sourceFilePath);
            return null;
        }

        var outputPath = PathUtils.NormalizeFilePath(ProjectSettings.GlobalizePath(importFilepath));

        Directory.CreateDirectory(Path.GetFullPath(outputPath.GetBaseDir()));
        var assetExisted = File.Exists(outputPath) && ResourceLoader.Exists(importFilepath);
        var updatedResource = await AsyncImporter.QueueAssetImport(sourceFilePath, outputPath, Game, ExecuteImport);
        resource.ImportedResource = updatedResource;
        if (WritesEnabled && !string.IsNullOrEmpty(resource.ResourcePath)) {
            ResourceSaver.Save(resource);
        }
        return updatedResource;
    }

    protected async Task ExecuteBlenderScript(string script, bool background, string outputFilepath)
    {
        var blenderPath = ReachForGodot.BlenderPath;
        if (string.IsNullOrEmpty(blenderPath)) {
            if (!_hasShownNoBlenderWarning) {
                GD.PrintErr("Blender is not configured. Meshes and textures will not import.");
                _hasShownNoBlenderWarning = true;
            }
            return;
        }

        var process = new Process() { StartInfo = new ProcessStartInfo() {
            UseShellExecute = false,
            FileName = blenderPath,
            WindowStyle = background ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal,
            Arguments = background
                ? $"\"{EmptyBlend}\" --background --python-expr \"{script}\""
                : $"\"{EmptyBlend}\" --python-expr \"{script}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }};

        if (cancellationTokenSource == null || cancellationTokenSource.IsCancellationRequested) {
            cancellationTokenSource = new();
        }

        var output = new StringBuilder();
        process!.OutputDataReceived += (sender, e) => {
            var data = e.Data;
            if (!string.IsNullOrWhiteSpace(data)) {
                output.AppendLine(data.Trim());
            }
        };
        process.ErrorDataReceived += (sender, e) => {
            var data = e.Data;
            if (!string.IsNullOrWhiteSpace(data)) {
                output.Append("ERROR:").AppendLine(data.Trim());
            }
        };

        var delay = Task.Delay(blenderTimeoutMs, cancellationTokenSource.Token);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        var exitTask = process!.WaitForExitAsync(cancellationTokenSource.Token).ContinueWith(t => {
            if (!t.IsCompletedSuccessfully || !File.Exists(outputFilepath)) {
                GD.PrintErr("Blender conversion failed. Output:\n" + output.ToString());
                throw t.Exception ?? new Exception("Process failed");
            }
        });
        var completedTask = await Task.WhenAny(exitTask, delay);
        if (completedTask == delay) {
            cancellationTokenSource.Cancel();
            cancellationTokenSource = null;
        }
    }
}
