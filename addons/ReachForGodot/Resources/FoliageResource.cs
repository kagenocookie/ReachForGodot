namespace ReaGE;

using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool, ResourceHolder("fol", SupportedFileFormats.Foliage)]
public partial class FoliageResource : REResource, IExportableAsset, IImportableAsset
{
    [Export] public FoliageGroup[]? Groups;
    [Export] public Aabb Bounds;

    public FoliageResource() : base(SupportedFileFormats.Foliage)
    {
    }

    public bool IsEmpty => Groups == null || Groups.Length == 0;

    public async Task<bool> EnsureImported(bool saveResource)
    {
        if (!await CreateImporter().Fol.ImportFromFile(this)) return false;
        if (saveResource) {
            ResourceSaver.Save(this);
        }
        return true;
    }
}
