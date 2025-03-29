namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, Tool, ResourceHolder("fol", RESupportedFileFormats.Foliage)]
public partial class FoliageResource : REResource, IExportableAsset, IImportableAsset
{
    [Export] public FoliageGroup[]? Groups;
    [Export] public Aabb Bounds;

    public FoliageResource() : base(RESupportedFileFormats.Foliage)
    {
    }

    public bool IsEmpty => Groups == null || Groups.Length == 0;

    public async Task<bool> EnsureImported(bool saveResource)
    {
        var resolved = PathUtils.FindSourceFilePath(Asset?.AssetFilename, ReachForGodot.GetAssetConfig(Game));
        if (resolved == null) return false;
        await Import(resolved, null);
        if (saveResource) {
            ResourceSaver.Save(this);
        }
        return true;
    }

    public Task<bool> Import(string resolvedFilepath, GodotRszImporter? importer)
    {
        using var file = new FolFile(new FileHandler(resolvedFilepath));
        file.Read();
        Bounds = file.aabb.ToGodot();
        var config = ReachForGodot.GetAssetConfig(Game);
        Groups = file.InstanceGroups?.Select(grp => new FoliageGroup() {
            Material = Importer.FindOrImportResource<MaterialResource>(grp.materialPath, config),
            Mesh = Importer.FindOrImportResource<MeshResource>(grp.meshPath, config),
            Transforms = grp.transforms == null ? new() : new(grp.transforms.Select(c => c.ToGodot())),
            Bounds = grp.aabb.ToGodot(),
        }).ToArray();
        return Task.FromResult(true);
    }
}
