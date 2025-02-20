namespace RGE;

using System;
using System.Diagnostics;
using Godot;
using RszTool;

[GlobalClass, Tool]
public partial class SceneFolder : Node, IRszContainerNode
{
    [Export] public SupportedGame Game { get; set; }
    [Export] public AssetReference? Asset { get; set; }
    [Export] public REResource[]? Resources { get; set; }
    [Export] public int ObjectId { get; set; }

    public bool IsEmpty => GetChildCount() == 0;

    public Node? FolderContainer { get; private set; }

    [ExportToolButton("Regenerate tree")]
    private Callable BuildTreeButton => Callable.From(BuildTree);

    [ExportToolButton("Regenerate tree + Children (can take a while)")]
    private Callable BuildFullTreeButton => Callable.From(BuildTreeDeep);

    [ExportToolButton("Show source file")]
    private Callable OpenSourceFile => Callable.From(() => Asset?.OpenSourceFile(Game));

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
        var conv = new RszGodotConverter(ReachForGodot.GetAssetConfig(Game!)!, false);
        conv.GenerateSceneTree(this);
    }

    public void BuildTreeDeep()
    {
        var conv = new RszGodotConverter(ReachForGodot.GetAssetConfig(Game!)!, true);
        conv.GenerateSceneTree(this);
    }
}