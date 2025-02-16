namespace RFG;

using System;
using System.Diagnostics;
using Godot;
using RszTool;

[GlobalClass, Tool]
public partial class SceneFolder : Node, IRszContainerNode
{
    [Export] public string? Game { get; set; }
    [Export] public AssetReference? Asset { get; set; }
    [Export] public REResource[]? Resources { get; set; }
    [Export] public int ObjectId { get; set; }

    public bool IsEmpty => GetChildCount() == 0;

    public Node? FolderContainer { get; private set; }

    [ExportToolButton("Regenerate tree")]
    private Callable BuildTreeButton => Callable.From(BuildTree);

    [ExportToolButton("Regenerate tree + Children")]
    private Callable BuildFullTreeButton => Callable.From(BuildTreeDeep);

    [ExportToolButton("Open source file")]
    private Callable OpenSourceFile => Callable.From(() => ((IRszContainerNode)this).OpenSourceFile());

    [ExportToolButton("Find me something to look at")]
    public Callable Find3DNodeButton => Callable.From(() => ((IRszContainerNode)this).Find3DNode());

    public void Clear()
    {
        FolderContainer = null;
        this.FreeAllChildrenImmediately();
    }

    public void AddFolder(SceneFolder folder)
    {
        if (FolderContainer == null) {
            AddChild(FolderContainer = new Node() { Name = "Folders" });
            FolderContainer.Owner = this;
        }
        FolderContainer.AddChild(folder);
        folder.Owner = Owner ?? this;
    }

    public void BuildTree()
    {
        using var conv = new GodotScnConverter(ReachForGodot.GetAssetConfig(Game!)!, false);
        conv.GenerateSceneTree(this);
    }

    public void BuildTreeDeep()
    {
        using var conv = new GodotScnConverter(ReachForGodot.GetAssetConfig(Game!)!, true);
        conv.GenerateSceneTree(this);
    }
}