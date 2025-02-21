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
    public IEnumerable<SceneFolder> Subfolders => FolderContainer?.FindChildrenByType<SceneFolder>() ?? Array.Empty<SceneFolder>();

    [ExportToolButton("Basic import (rebuild current tree; linked assets unchanged)")]
    private Callable BuildTreeButton => Callable.From(() => BuildTree(RszGodotConverter.placeholderImport));

    [ExportToolButton("Import what isn't")]
    private Callable BuildImportTreeButton => Callable.From(() => BuildTree(RszGodotConverter.importMissing));

    [ExportToolButton("Reimport changes to tree and children (coffee break time)")]
    private Callable BuildFullTreeButton => Callable.From(() => BuildTree(RszGodotConverter.importTreeChanges));

    [ExportToolButton("Discard local data; full rebuild incl meshes (lunch break time)")]
    private Callable BuildFullButton => Callable.From(() => BuildTree(RszGodotConverter.fullReimport));

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