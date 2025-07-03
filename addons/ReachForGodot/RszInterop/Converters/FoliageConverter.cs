namespace ReaGE;

using System.Threading.Tasks;
using ReeLib;

public class FoliageConverter : ResourceConverter<FoliageResource, FolFile>
{
    public override FolFile CreateFile(FileHandler fileHandler) => new FolFile(fileHandler);

    public override Task<bool> Import(FolFile file, FoliageResource target)
    {
        target.Bounds = file.aabb.ToGodot();
        var config = ReachForGodot.GetAssetConfig(Game);
        target.Groups = file.InstanceGroups?.Select(grp => new FoliageGroup() {
            Material = Importer.FindOrImportResource<MaterialDefinitionResource>(grp.materialPath, config, WritesEnabled),
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
