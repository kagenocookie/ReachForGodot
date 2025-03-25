namespace ReaGE;

using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool]
public partial class RcolRootNode : Node, IExportableAsset, IImportableAsset
{
    [Export] public SupportedGame Game { get; set; }
    [Export] public AssetReference? Asset { get; set; }

    public RcolResource? Resource => Importer.FindOrImportResource<RcolResource>(Asset, ReachForGodot.GetAssetConfig(Game));

    public IEnumerable<RequestSetCollisionGroup> Groups => this.FindChild("Groups", false).FindChildrenByType<RequestSetCollisionGroup>();
    public IEnumerable<RequestSetCollider> Sets => this.FindChildrenByType<RequestSetCollider>();

    bool IImportableAsset.IsEmpty => this.Resource?.IsEmpty != false;

    public void HideGroupsExcept(RequestSetCollider set)
    {
        HideGroupsExcept(set.Group);
    }
    public void HideGroupsExcept(RequestSetCollisionGroup? showGroup)
    {
        foreach (var group in Groups) {
            group.Visible = group == showGroup;
        }
    }

    Task<bool> IImportableAsset.Import(string resolvedFilepath, GodotRszImporter importer)
    {
        importer.GenerateRcol(this);
        NotifyPropertyListChanged();
        return Task.FromResult(true);
    }
}
