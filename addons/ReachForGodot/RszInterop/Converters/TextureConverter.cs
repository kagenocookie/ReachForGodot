namespace ReaGE;

using System.Diagnostics;
using System.Threading.Tasks;
using Godot;
using ReeLib.Common;

public class TextureConverter : BlenderResourceConverter<TextureResource, Texture2D>
{
    private static string? _script;
    private static string ImportScript => _script ??= File.ReadAllText(ProjectSettings.GlobalizePath("res://addons/ReachForGodot/scripts/import_tex.py"));

    private static string? _texconvPath;
    private static string TexconvFilepath => _texconvPath ??= ProjectSettings.GlobalizePath("res://addons/ReachForGodot/Tools/texconv.exe");

    private static readonly float log2 = Mathf.Log(2f);

    public override TextureResource CreateOrReplaceResourcePlaceholder(AssetReference reference)
        => SetupResource(new TextureResource(), reference);

    protected override Task<bool> ExecuteImport(string sourceFilePath, string outputPath)
    {
        var convertedFilepath = sourceFilePath.GetBaseName().GetBaseName() + ".dds";

        var script = ImportScript
            .Replace("__FILEPATH__", sourceFilePath)
            .Replace("__FILEDIR__", sourceFilePath.GetBaseDir())
            .Replace("__FILENAME__", sourceFilePath.GetFile());

        return ExecuteBlenderScript(script, true, convertedFilepath).ContinueWith((t) => {
            if (t.IsCompletedSuccessfully && File.Exists(convertedFilepath)) {
                PostprocessDDS(convertedFilepath);
                if (convertedFilepath != outputPath) {
                    File.Move(convertedFilepath, outputPath, true);
                }
                return true;
            } else {
                // array textures and supported stuff... not sure how to handle those
                return false;
            }
        });
    }

    private static void PostprocessDDS(string filepath)
    {
        // add any missing mipmaps since godot doesn't know how to handle those yet
        var texconv = TexconvFilepath;
        if (!File.Exists(texconv)) return;

        using var stream = File.OpenRead(filepath);
        var header = new DDSHeader();
        stream.Read(MemoryUtils.StructureAsBytes(ref header));
        stream.Close();
        var expectedMips = (uint)Mathf.RoundToInt(Mathf.Max(Mathf.Log(header.width) / log2, Mathf.Log(header.height) / log2)) + 1;
        if (header.mipMapCount != expectedMips) {
            Process.Start(new ProcessStartInfo(texconv) {
                WorkingDirectory = filepath.GetBaseDir(),
                Arguments = $"\"{filepath}\" -m {expectedMips} -y",
            })?.WaitForExit();
        }
    }

    private struct DDSHeader
    {
        public uint magic;
        public uint size;
        public uint flags;
        public uint height;
        public uint width;
        public uint pitchOrLinearSize;
        public uint depth;
        public uint mipMapCount;
    }
}
