namespace RFG;

using System;
using System.Diagnostics;
using Godot;
using RszTool;

[GlobalClass, Tool]
public partial class SceneNodeRoot : Node
{
    [Export] public string? Game { get; set; }
    [Export] public AssetReference? Asset { get; set; }
    [Export] public string[]? Resources { get; set; }

    [ExportToolButton("Open source file")]
    private Callable OpenSourceFile => Callable.From(() => {
        Process.Start(new ProcessStartInfo("explorer.exe") {
            UseShellExecute = false,
            Arguments = "/select, \"" + Path.Join(ReachForGodot.GetPaths(Game!)!.ChunkPath, Asset!.AssetFilename).Replace('/', '\\') + '"',
        });
    });

    public Node? FolderContainer { get; private set; }

    [ExportToolButton("Regenerate tree")]
    private Callable BuildTreeButton => Callable.From(BuildTree);

    public static readonly Dictionary<string, SceneNodeRoot> activeScenes = new();

    public override void _EnterTree()
    {
        if (Asset?.AssetFilename != null) {
            activeScenes.Add(Asset.AssetFilename, this);
        }
    }

    public override void _ExitTree()
    {
        if (Asset?.AssetFilename != null) {
            activeScenes.Remove(Asset.AssetFilename);
        }
    }

    public void Clear()
    {
        FolderContainer = null;
        this.FreeAllChildrenImmediately();
    }

    public void AddFolder(REFolder folder, REFolder? parent)
    {
        if (FolderContainer == null) {
            AddChild(FolderContainer = new Node() { Name = "Folders" });
            FolderContainer.Owner = this;
        }
        if (parent == null) {
            FolderContainer.AddChild(folder);
        } else {
            parent.AddChild(folder);
        }
        folder.Owner = this;
    }

    public void AddGameObject(REGameObject gameObject, REGameObject? parent)
    {
        if (parent != null) {
            parent.EnsureChildContainerSetup().AddUniqueNamedChild(gameObject);
        } else {
            this.AddUniqueNamedChild(gameObject);
        }

        gameObject.Owner = this;
    }

    public void BuildTree()
    {
        GodotScnConverter.EnsureSafeJsonLoadContext();
        var conv = new GodotScnConverter(ReachForGodot.GetPaths(Game!)!);
        conv.GenerateSceneTree(this);
    }

    public REFolder? FindFolderById(int objectId) => this.FindChildWhereRecursive<REFolder>(c => c.ObjectId == objectId);
}