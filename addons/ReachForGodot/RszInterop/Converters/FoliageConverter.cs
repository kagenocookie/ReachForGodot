namespace ReaGE;

using System.Threading.Tasks;
using RszTool;

public class FoliageConverter : ResourceConverter<FoliageResource, FolFile>
{
    public override FoliageResource CreateOrReplaceResourcePlaceholder(AssetReference reference)
        => SetupResource(new FoliageResource(), reference);

    public override FolFile CreateFile(FileHandler fileHandler) => new FolFile(fileHandler);

    public override Task<bool> Import(FolFile file, FoliageResource target)
    {
        file.Read();
        target.Bounds = file.aabb.ToGodot();
        var config = ReachForGodot.GetAssetConfig(Game);
        target.Groups = file.InstanceGroups?.Select(grp => new FoliageGroup() {
            Material = Importer.FindOrImportResource<MaterialResource>(grp.materialPath, config, WritesEnabled),
            Mesh = Importer.FindOrImportResource<MeshResource>(grp.meshPath, config, WritesEnabled),
            Transforms = grp.transforms == null ? new() : new(grp.transforms.Select(c => c.ToGodot())),
            Bounds = grp.aabb.ToGodot(),
        }).ToArray();
        return Task.FromResult(true);
    }

    public override Task<bool> Export(FoliageResource source, FolFile file)
    {
        return Task.FromResult(false);
    }
}
