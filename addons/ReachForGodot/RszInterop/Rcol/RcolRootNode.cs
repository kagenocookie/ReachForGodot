namespace ReaGE;

using System;
using Godot;

[GlobalClass, Tool]
public partial class RcolRootNode : Node3D, IAssetPointer
{
    [Export] public SupportedGame Game { get; set; }
    [Export] public AssetReference? Asset { get; set; }

    public RcolResource? Resource => Importer.FindOrImportResource<RcolResource>(Asset, ReachForGodot.GetAssetConfig(Game));
}
