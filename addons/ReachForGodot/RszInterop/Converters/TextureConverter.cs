namespace ReaGE;

using System.Threading.Tasks;
using Godot;

public class TextureConverter : BlenderResourceConverter<TextureResource, Texture2D>
{
    private static string? _script;
    private static string ImportScript => _script ??= File.ReadAllText(ProjectSettings.GlobalizePath("res://addons/ReachForGodot/scripts/import_tex.py"));

    public override TextureResource CreateOrReplaceResourcePlaceholder(AssetReference reference)
        => SetupResource(new TextureResource(), reference);

    protected override Task<bool> ExecuteImport(string sourceFilePath, string outputPath)
    {
        var convertedFilepath = sourceFilePath.GetBaseName().GetBaseName() + ".dds";

        var script = ImportScript
            .Replace("__FILEPATH__", sourceFilePath)
            .Replace("__FILEDIR__", sourceFilePath.GetBaseDir())
            .Replace("__FILENAME__", sourceFilePath.GetFile());

        return ExecuteBlenderScript(script, true).ContinueWith((t) => {
            if (t.IsCompletedSuccessfully && File.Exists(convertedFilepath)) {
                if (convertedFilepath != outputPath) {
                    File.Move(convertedFilepath, outputPath, true);
                }
                QueueFileRescan();
                return true;
            } else {
                // array textures and supported stuff... not sure how to handle those
                return false;
            }
        });
    }
}
