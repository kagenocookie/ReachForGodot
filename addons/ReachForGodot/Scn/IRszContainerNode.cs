namespace RFG;

using System;
using System.Diagnostics;
using Godot;
using JetBrains.Annotations;
using RszTool;

public interface IRszContainerNode
{
    public string? Game { get; set; }
    public AssetReference? Asset { get; set; }
    public REResource[]? Resources { get; set; }
    public int ObjectId { get; set; }

    public bool IsEmpty { get; }

    public void OpenSourceFile()
    {
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
    }

    [ExportToolButton("Find me something to look at")]
    public Callable Find3DNodeButton => Callable.From(Find3DNode);

    public void Clear()
    {
        (this as Node)?.FreeAllChildrenImmediately();
    }

    public void AddGameObject(REGameObject gameObject, REGameObject? parent)
    {
        if (parent != null) {
            parent.AddUniqueNamedChild(gameObject);
        } else {
            (this as Node)?.AddUniqueNamedChild(gameObject);
        }

        gameObject.Owner = (this as Node)?.Owner ?? this as Node;
    }

    public void Find3DNode()
    {
        if (this is Node thisnode) {
            var cam = EditorInterface.Singleton.GetEditorViewport3D().GetCamera3D();
            if (cam != null) {
                var node = thisnode.FindChildWhereRecursive<Node3D>(child => child is VisualInstance3D vis && vis.GetAabb().Size != Vector3.Zero);
                var aabb = node == null ? thisnode.GetNode3DAABB(true) : node.GetNode3DAABB(false);
                if (aabb.Size == Vector3.Zero) {
                    node = thisnode.FindChildByTypeRecursive<Node3D>();
                    var center = node?.GlobalPosition ?? aabb.GetCenter();
                    cam.LookAtFromPosition(new Vector3(5, 5, 5), center);
                } else {
                    cam.LookAtFromPosition(aabb.GetCenter() + aabb.Size.LimitLength(50), aabb.GetCenter());
                }
            }
        }
    }
}