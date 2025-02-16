namespace RFG;

using System;
using System.Diagnostics;
using Godot;
using RszTool;

[GlobalClass, Tool]
public partial class PrefabNode : RszContainerNode
{
    public Node? FolderContainer { get; private set; }

    [ExportToolButton("Regenerate tree")]
    private Callable BuildTreeButton => Callable.From(BuildTree);

    [ExportToolButton("Regenerate tree + Children")]
    private Callable BuildFullTreeButton => Callable.From(BuildTreeDeep);

    public override void Clear()
    {
        FolderContainer = null;
        base.Clear();
    }

    public void BuildTree()
    {
        using var conv = new GodotScnConverter(ReachForGodot.GetAssetConfig(Game!)!, false);
        conv.GeneratePrefabTree(this);
    }

    public void BuildTreeDeep()
    {
        using var conv = new GodotScnConverter(ReachForGodot.GetAssetConfig(Game!)!, true);
        conv.GeneratePrefabTree(this);
    }
}