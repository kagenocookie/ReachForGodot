namespace ReaGE;

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool, Icon("res://addons/ReachForGodot/icons/folder_instance.png")]
public partial class SceneFolderEditableInstance : SceneFolder
{
    [ExportToolButton("Save instance")]
    private Callable SaveInstance => Callable.From(DoSaveInstance);

    private void DoSaveInstance()
    {
        if (string.IsNullOrEmpty(Asset?.AssetFilename)) {
            GD.PrintErr("Asset filename field is missing");
            return;
        }

        var importPath = Asset.GetImportFilepath(ReachForGodot.GetAssetConfig(Game));

        var res = ResourceLoader.Exists(importPath) ? ResourceLoader.Load<PackedScene>(importPath) : new PackedScene();
        var clone = new SceneFolder() { Name = Name };
        clone.CopyDataFrom(this);
        foreach (var child in GetChildren()) {
            var childClone = child.Duplicate();
            clone.AddChild(childClone);
            childClone.SetRecursiveOwner(clone, this);
        }

        res.Pack(clone);
        if (string.IsNullOrEmpty(res.ResourcePath)) {
            res.ResourcePath = importPath;
        } else {
            res.TakeOverPath(importPath);
        }
        ResourceSaver.Save(res);
        GD.Print("Updated scene resource: " + importPath);
    }
}