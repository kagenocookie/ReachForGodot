namespace ReaGE;

using Godot;

public static class EditorExtensions
{
    public static void SetPropertyUndoable(this EditorProperty prop, GodotObject target, StringName property, Variant after, string? actionName = null)
    {
        var undo = EditorInterface.Singleton.GetEditorUndoRedo();
        undo.CreateAction(actionName ?? $"Set {property}" , UndoRedo.MergeMode.Ends, target);
        undo.AddUndoProperty(target, property, target.Get(property));
        undo.AddDoProperty(target, property, after);
        undo.CommitAction();
    }

    public static void SetPropertyUndoable(GodotObject target, StringName property, Variant after, string? actionName = null)
    {
        var undo = EditorInterface.Singleton.GetEditorUndoRedo();
        undo.CreateAction(actionName ?? $"Set {property}", UndoRedo.MergeMode.Ends, target);
        undo.AddUndoProperty(target, property, target.Get(property));
        undo.AddDoProperty(target, property, after);
        undo.CommitAction();
    }

    public static PackedScene ToPackedScene(this Node node, bool fixOwners = true)
    {
        var scene = new PackedScene();
        scene.Pack(node);
        if (fixOwners) node.SetRecursiveOwner(node);
        return scene;
    }

    public static PackedScene SaveAsScene(this Node node, string importPath)
    {
        var scene = ToPackedScene(node);
        var err = scene.SaveOrReplaceResource(importPath);
        if (err != Error.Ok) {
            GD.PrintErr("Failed to save scene: " + err);
        }
        return scene;
    }

    public static Error SaveOrReplaceResource(this Resource resource, string importFilepath)
    {
        if (ResourceLoader.Exists(importFilepath) && resource.ResourcePath != importFilepath) {
            resource.TakeOverPath(importFilepath);
        } else {
            Directory.CreateDirectory(ProjectSettings.GlobalizePath(importFilepath).GetBaseDir());
            resource.ResourcePath = importFilepath;
        }
        var status = ResourceSaver.Save(resource);
        if (status != Error.Ok) {
            GD.PrintErr("Failed to save resource: " + status);
        }
        return status;
    }
}