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
        target.Layer = file.LayerGuid.ToString();
        target.MaskGuids = file.MaskGuids ?? Array.Empty<Guid>();
        return Task.FromResult(true);
    }

    public override Task<bool> Export(CollisionFilterResource source, CfilFile file)
    {
        file.LayerGuid = Guid.TryParse(source.Layer, out var guid) ? guid : Guid.Empty;
        file.MaskGuids = source.MaskGuids;
        return Task.FromResult(true);
    }
}
