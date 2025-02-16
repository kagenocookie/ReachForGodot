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
        var conv = new GodotScnConverter(ReachForGodot.GetAssetConfig(Game!)!);
        conv.GenerateSceneTree(this);
    }

    public SceneFolder? FindFolderById(int objectId) => this.FindChildWhereRecursive<SceneFolder>(c => c.ObjectId == objectId);
}