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

    private bool childrenVisible = true;
    [Export] public bool ShowChildren {
        get => childrenVisible;
        set => SetChildVisibility(value);
    }

    public bool IsEmpty => GetChildCount() == 0;

    public Node? FolderContainer { get; private set; }
    public IEnumerable<SceneFolder> Subfolders => FolderContainer?.FindChildrenByType<SceneFolder>() ?? Array.Empty<SceneFolder>();

    public void Clear()
    {
        FolderContainer = null;
        this.FreeAllChildrenImmediately();
    }

    public void SetChildVisibility(bool visible)
    {
        childrenVisible = visible;
        foreach (var ch in this.FindChildrenByTypeRecursive<Node3D>()) {
            ch.Visible = childrenVisible;
        }
        if (!visible) {
            this.SetDisplayFolded(true);
        }
    }

    public override void _EnterTree()
    {
        if (!childrenVisible) {
            SetChildVisibility(false);
        }
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

    public void BuildTree(RszGodotConversionOptions options)
    {
        var conv = new RszGodotConverter(ReachForGodot.GetAssetConfig(Game!)!, options);
        conv.GenerateSceneTree(this).ContinueWith((t) => {
            if (t.IsFaulted) {
                GD.Print("Tree rebuild failed:", t.Exception);
            } else {
                GD.Print("Tree rebuild finished");
            }
            EditorInterface.Singleton.CallDeferred(EditorInterface.MethodName.MarkSceneAsUnsaved);
        });
    }
}