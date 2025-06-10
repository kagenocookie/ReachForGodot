namespace ReaGE;

using System.Threading.Tasks;
using RszTool;

public class HfConverter : ResourceConverter<HeightFieldResource, HFFile>
{
    public override HFFile CreateFile(FileHandler fileHandler) => new HFFile(fileHandler);

    public override Task<bool> Import(HFFile file, HeightFieldResource target)
    {
        return Task.FromResult(false);
    }

    public override Task<bool> Export(HeightFieldResource source, HFFile file)
    {
        return Task.FromResult(false);
    }
}
