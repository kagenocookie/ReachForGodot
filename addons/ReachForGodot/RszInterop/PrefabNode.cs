namespace RFG;

using System;
using System.Diagnostics;
using Godot;
using RszTool;

[GlobalClass, Tool]
public partial class PrefabNode : REGameObject, IRszContainerNode
{
    [Export] public string? Game { get; set; }
    [Export] public AssetReference? Asset { get; set; }
    [Export] public REResource[]? Resources { get; set; }

    public bool IsEmpty => GetChildCount() == 0;

    [ExportToolButton("Regenerate tree")]
    private Callable BuildTreeButton => Callable.From(BuildTree);

    [ExportToolButton("Regenerate tree + Children")]
    private Callable BuildFullTreeButton => Callable.From(BuildTreeDeep);

    [ExportToolButton("Open source file")]
    private Callable OpenSourceFile => Callable.From(() => ((IRszContainerNode)this).OpenSourceFile());

    [ExportToolButton("Find me something to look at")]
    public Callable Find3DNodeButton => Callable.From(() => ((IRszContainerNode)this).Find3DNode());

    public void BuildTree()
    {
        using var conv = new RszGodotConverter(ReachForGodot.GetAssetConfig(Game!)!, false);
        conv.GeneratePrefabTree(this);
    }

    public void BuildTreeDeep()
    {
        using var conv = new RszGodotConverter(ReachForGodot.GetAssetConfig(Game!)!, true);
        conv.GeneratePrefabTree(this);
    }
}