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

public static class EditorResources
{
    private const string NavmeshMaterial1Path = "res://addons/ReachForGodot/Editor/Gizmo/navmesh1.material";
    private static Material? _navmeshMat1;
    public static Material NavmeshMaterial1 => _navmeshMat1 ??= ResourceLoader.Load<Material>(NavmeshMaterial1Path);

    private const string NavmeshMaterial2Path = "res://addons/ReachForGodot/Editor/Gizmo/navmesh2.material";
    private static Material? _navmeshMat2;
    public static Material NavmeshMaterial2 => _navmeshMat2 ??= ResourceLoader.Load<Material>(NavmeshMaterial2Path);

    private const string McolMaterialPath = "res://addons/ReachForGodot/Editor/Gizmo/mcol.material";
    private static Material? _mcolMat;
    public static Material McolMaterial => _mcolMat ??= ResourceLoader.Load<Material>(McolMaterialPath);

    public static readonly StringName IgnoredSceneGroup = "RFGIgnore";

    public static readonly Color[] LayerColors = [
        Colors.White, Colors.Blue, Colors.Green, Colors.Red, Colors.Magenta,
        Colors.Yellow, Colors.AliceBlue, Colors.AntiqueWhite, Colors.Aqua, Colors.Aquamarine,
        Colors.Beige, Colors.Bisque, Colors.BlanchedAlmond, Colors.BlueViolet, Colors.Brown,
        Colors.Burlywood, Colors.CadetBlue, Colors.Chartreuse, Colors.Chocolate, Colors.Coral,
        Colors.CornflowerBlue, Colors.Cornsilk, Colors.Crimson, Colors.Cyan, Colors.DarkBlue,
        Colors.DarkCyan, Colors.DarkGoldenrod, Colors.DarkGray, Colors.DarkGreen, Colors.DarkKhaki,
        Colors.DarkMagenta, Colors.DarkOliveGreen, Colors.DarkOrange, Colors.DarkOrchid, Colors.DarkRed,
        Colors.DarkSalmon, Colors.DarkSeaGreen
    ];
}
