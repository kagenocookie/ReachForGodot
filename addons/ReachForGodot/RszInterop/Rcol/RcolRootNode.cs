namespace ReaGE;

using System;
using Godot;

[GlobalClass, Tool]
public partial class RcolRootNode : Node, IAssetPointer
{
    [Export] public SupportedGame Game { get; set; }
    [Export] public AssetReference? Asset { get; set; }

    public RcolResource? Resource => Importer.FindOrImportResource<RcolResource>(Asset, ReachForGodot.GetAssetConfig(Game));
}
