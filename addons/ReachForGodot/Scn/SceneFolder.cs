namespace RFG;

using System;
using System.Diagnostics;
using Godot;
using RszTool;

[GlobalClass, Tool]
public partial class SceneFolder : Node
{
    [Export] public string? Game { get; set; }
    [Export] public AssetReference? Asset { get; set; }
    [Export] public REResource[]? Resources { get; set; }
    [Export] public int ObjectId { get; set; } = -1;

    public bool IsEmpty => GetChildCount() == 0;

    [ExportToolButton("Open source file")]
    private Callable OpenSourceFile => Callable.From(() => {
        if (Asset == null) {
            GD.PrintErr("Scene does not have a source asset defined");
            return;
        }

        string file = Importer.ResolveSourceFilePath(Asset.AssetFilename, ReachForGodot.GetAssetConfig(Game)).Replace('/', '\\');
        if (File.Exists(file)) {
            Process.Start(new ProcessStartInfo("explorer.exe") {
                UseShellExecute = false,
                Arguments = $"/select, \"{file}\"",
            });
        }
    });

    public Node? FolderContainer { get; private set; }

    [ExportToolButton("Regenerate tree")]
    private Callable BuildTreeButton => Callable.From(BuildTree);

    [ExportToolButton("Regenerate tree + Children")]
    private Callable BuildFullTreeButton => Callable.From(BuildTreeDeep);

    [ExportToolButton("Find me something to look at")]
    private Callable Find3DNodeButton => Callable.From(Find3DNode);

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

    public void AddGameObject(REGameObject gameObject, REGameObject? parent)
    {
        if (parent != null) {
            parent.EnsureChildContainerSetup().AddUniqueNamedChild(gameObject);
        } else {
            this.AddUniqueNamedChild(gameObject);
        }

        gameObject.Owner = Owner ?? this;
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

    private void Find3DNode()
    {
        var cam = EditorInterface.Singleton.GetEditorViewport3D().GetCamera3D();
        if (cam != null) {
            var node = this.FindChildWhereRecursive<Node3D>(child => child is VisualInstance3D vis && vis.GetAabb().Size != Vector3.Zero);
            var aabb = node == null ? this.GetNode3DAABB(true) : node.GetNode3DAABB(false);
            if (aabb.Size == Vector3.Zero) {
                cam.LookAtFromPosition(new Vector3(5, 5, 5), aabb.GetCenter());
            } else {
                cam.LookAtFromPosition(aabb.GetCenter() + aabb.Size.LimitLength(50), aabb.GetCenter());
            }
        }
    }

    public SceneFolder? FindFolderById(int objectId) => this.FindChildWhereRecursive<SceneFolder>(c => c.ObjectId == objectId);
}