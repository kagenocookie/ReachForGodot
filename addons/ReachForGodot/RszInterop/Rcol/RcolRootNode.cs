namespace ReaGE;

using System;
using Godot;

[GlobalClass, Tool]
public partial class RcolRootNode : Node, IAssetPointer
{
    [Export] public SupportedGame Game { get; set; }
    [Export] public AssetReference? Asset { get; set; }

    public RcolResource? Resource => Importer.FindOrImportResource<RcolResource>(Asset, ReachForGodot.GetAssetConfig(Game));

    public IEnumerable<RigidCollisionGroup> Groups => this.FindChild("Groups", false).FindChildrenByType<RigidCollisionGroup>();
    public IEnumerable<RigidCollisionRequestSet> Sets => this.FindChildrenByType<RigidCollisionRequestSet>();

    public void HideGroupsExcept(RigidCollisionRequestSet set)
    {
        HideGroupsExcept(set.Group);
    }
    public void HideGroupsExcept(RigidCollisionGroup? showGroup)
    {
        foreach (var group in Groups) {
            group.Visible = group == showGroup;
        }
    }
}
