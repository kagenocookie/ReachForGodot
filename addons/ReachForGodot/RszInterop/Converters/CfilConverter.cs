namespace ReaGE;

using System.Threading.Tasks;
using RszTool;

public class CfilConverter : ResourceConverter<CollisionFilterResource, CfilFile>
{
    public override CollisionFilterResource CreateOrReplaceResourcePlaceholder(AssetReference reference)
        => SetupResource(new CollisionFilterResource(), reference);

    public override CfilFile CreateFile(FileHandler fileHandler) => new CfilFile(fileHandler);

    public override Task<bool> Import(CfilFile file, CollisionFilterResource target)
    {
        file.Read();
        target.Uuid = file.myGuid.ToString();
        target.CollisionGuids = file.Guids?.Select(g => g.ToString()).ToArray();
        return Task.FromResult(true);
    }

    public override Task<bool> Export(CollisionFilterResource source, CfilFile file)
    {
        file.myGuid = Guid.TryParse(source.Uuid, out var guid) ? guid : Guid.Empty;
        file.Guids = source.CollisionGuids?.Select(g => Guid.TryParse(g, out var guid) ? guid : Guid.Empty).ToArray() ?? Array.Empty<Guid>();
        return Task.FromResult(true);
    }
}
