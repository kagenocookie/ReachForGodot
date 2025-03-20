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
}