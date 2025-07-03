namespace ReaGE;

using System.Threading.Tasks;
using ReeLib;

public class CmatConverter : ResourceConverter<CollisionMaterialResource, CmatFile>
{
    public override CmatFile CreateFile(FileHandler fileHandler) => new CmatFile(fileHandler);

    public override Task<bool> Import(CmatFile file, CollisionMaterialResource target)
    {
        target.MaterialGuid = file.materialGuid;
        target.AttributeGuids = file.Attributes ?? Array.Empty<Guid>();
        return Task.FromResult(true);
    }

    public override Task<bool> Export(CollisionMaterialResource source, CmatFile file)
    {
        file.materialGuid = source.MaterialGuid;
        file.Attributes = source.AttributeGuids;
        return Task.FromResult(true);
    }
}
