namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using RszTool;

public class AimpConverter : RszAssetConverter<AiMapResource, AimpFile, AiMapResource, AiMapResource>
{
    public override AimpFile CreateFile(FileHandler fileHandler) => new AimpFile(FileOption, fileHandler);

    public override Task<bool> Import(AimpFile file, AiMapResource target)
    {
        // no actual import yet - still figuring out if these are importable or we just use the raw files
        return Task.FromResult(false);
    }

    public override Task<bool> Export(AiMapResource source, AimpFile file)
    {
        return Task.FromResult(false);
    }
}
