namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class RcolRootNode : Node, IExportableAsset, IImportableAsset
{
    [Export] public SupportedGame Game { get; set; }
    [Export] public AssetReference? Asset { get; set; }

    [Export] public string[]? IgnoreTags { get; set; }

    public RcolResource? Resource => Importer.FindOrImportResource<RcolResource>(Asset, ReachForGodot.GetAssetConfig(Game), !string.IsNullOrEmpty(SceneFilePath));

    public IEnumerable<RequestSetCollisionGroup> Groups => this.FindChild("Groups", false).FindChildrenByType<RequestSetCollisionGroup>();
    public IEnumerable<RequestSetCollider> Sets => this.FindChildrenByType<RequestSetCollider>();

    bool IImportableAsset.IsEmpty => this.GetChildCount() == 0;

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
