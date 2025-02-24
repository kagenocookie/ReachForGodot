namespace RGE;

using System;
using System.Diagnostics;
using Godot;

[GlobalClass, Tool]
public partial class SceneFolder : Node, IRszContainerNode
{
    [Export] public SupportedGame Game { get; set; }
    [Export] public AssetReference? Asset { get; set; }
    [Export] public REResource[]? Resources { get; set; }
    [Export] public Node? FolderContainer { get; private set; }

    private bool childrenVisible = true;
    [Export] public bool ShowChildren {
        get => childrenVisible;
        set => SetChildVisibility(value);
    }

    public bool IsEmpty => GetChildCount() == 0;

    public IEnumerable<SceneFolder> Subfolders => FolderContainer?.FindChildrenByType<SceneFolder>() ?? Array.Empty<SceneFolder>();

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
        if ((FolderContainer ??= FindChild("Folders")) == null) {
            AddChild(FolderContainer = new Node() { Name = "Folders" });
            FolderContainer.Owner = this;
        }
        FolderContainer.AddChild(folder);
        folder.Owner = Owner ?? this;
    }

    public void RemoveFolder(SceneFolder folder)
    {
        if (FolderContainer != null && folder.GetParent() == FolderContainer) {
            FolderContainer.RemoveChild(folder);
            folder.QueueFree();
        }
    }

    public SceneFolder? GetFolder(string name)
    {
        return FolderContainer?.FindChildWhere<SceneFolder>(c => c.Name == name);
    }

    public void BuildTree(RszGodotConversionOptions options)
    {
        var sw = new Stopwatch();
        sw.Start();
        var conv = new RszGodotConverter(ReachForGodot.GetAssetConfig(Game!)!, options);
        conv.GenerateSceneTree(this).ContinueWith((t) => {
            if (t.IsFaulted) {
                GD.Print("Tree rebuild failed:", t.Exception);
            } else {
                GD.Print("Tree rebuild finished in " + sw.Elapsed);
            }
            EditorInterface.Singleton.CallDeferred(EditorInterface.MethodName.MarkSceneAsUnsaved);
        });
    }
}