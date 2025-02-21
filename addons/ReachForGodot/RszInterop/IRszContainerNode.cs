namespace RGE;

using System;
using System.Diagnostics;
using Godot;
using JetBrains.Annotations;
using RszTool;

public interface IRszContainerNode
{
    public SupportedGame Game { get; set; }
    public AssetReference? Asset { get; set; }
    public REResource[]? Resources { get; set; }
    public int ObjectId { get; set; }

    public bool IsEmpty { get; }

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

        gameObject.Owner = this as Node;
        // GD.Print("Set owner " + gameObject.Name + " to " + gameObject.Owner?.Name);
    }

    public T? FindResource<T>(string? filepath) where T : REResource
    {
        if (Resources == null || string.IsNullOrEmpty(filepath)) return null;
        foreach (var res in Resources) {
            if (res is T cast && cast.Asset?.IsSameAsset(filepath) == true) {
                return cast;
            }
        }
        return null;
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
                    cam.LookAtFromPosition(node!.GlobalPosition + aabb.GetCenter() + aabb.Size.LimitLength(50), node!.GlobalPosition + aabb.GetCenter());
                }
            }
        }
    }
}