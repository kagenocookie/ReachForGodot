namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class RcolRootNode : Node, IExportableAsset
{
    [Export] public SupportedGame Game { get; set; }
    [Export] public AssetReference? Asset { get; set; }

    public RcolResource? Resource => Importer.FindOrImportResource<RcolResource>(Asset, ReachForGodot.GetAssetConfig(Game));

    public IEnumerable<RequestSetCollisionGroup> Groups => this.FindChild("Groups", false).FindChildrenByType<RequestSetCollisionGroup>();
    public IEnumerable<RequestSetCollider> Sets => this.FindChildrenByType<RequestSetCollider>();

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
}
