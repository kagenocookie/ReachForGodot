namespace ReaGE;

using System;
using Godot;

public static class ObjectTemplateManager
{
    private static string TemplateSubfolder(ObjectTemplateType type)
    {
        var templateFolder = type switch {
            ObjectTemplateType.GameObject => "GameObject",
            ObjectTemplateType.Component => "Component",
            _ => "GameObjects",
        };
        return $"templates/{templateFolder}/";
    }
    public static string GetBaseTemplateFolder(ObjectTemplateType type, SupportedGame game)
        => GamePaths.GetGameConfigPath(game, TemplateSubfolder(type));

    public static string GetUserTemplateFolder(ObjectTemplateType type, SupportedGame game)
        => ReachForGodot.GetUserdataPath(TemplateSubfolder(type));

    public static string[] GetAvailableTemplates(ObjectTemplateType type, SupportedGame game)
    {
        return GetFilesInFolder(GetUserTemplateFolder(type, game))
            .Concat(GetFilesInFolder(GetBaseTemplateFolder(type, game)))
            .ToArray();
    }

    private static IEnumerable<string> GetFilesInFolder(string folder)
    {
        var fa = DirAccess.Open(folder);
        if (fa == null) {
            var err = DirAccess.GetOpenError();
            return Array.Empty<string>();
        }
        // ReachForGodot.GetUserdataFolder("templates/GameObject/");
        return fa.GetFiles().Where(f => f.EndsWith(".tscn")).Select(filename => Path.Combine(folder, filename));
    }

    public static GameObject? InstantiateGameobject(string chosenTemplate, Node? parent, Node? owner)
    {
        var source = ResourceLoader.Load<PackedScene>(chosenTemplate).Instantiate<Node>(PackedScene.GenEditState.Instance);
        GameObject? clone = null;
        if (source is GameObject go) {
            clone = go.Clone();
            parent?.AddUniqueNamedChild(clone);
        } else if (source is ObjectTemplateRoot template) {
            clone = template.GetTarget<GameObject>();
            template.RemoveChild(clone);
            clone.Owner = null;
            parent?.AddUniqueNamedChild(clone);
            template.ApplyProperties(clone);
            template.QueueFree();
        }
        if (clone == null) {
                GD.PrintErr("Invalid game object template - must be a GameObject: " + chosenTemplate);
                return null;
            }

        if (owner != null) {
            clone.SetRecursiveOwner(owner, clone);
        }
        return clone;
    }

    public static REComponent? InstantiateComponent(string chosenTemplate, GameObject target)
    {
        var sourceInstance = ResourceLoader.Load<PackedScene>(chosenTemplate).Instantiate<Node>(PackedScene.GenEditState.Instance);
        if (sourceInstance is not ComponentTemplate template) {
            GD.PrintErr("Invalid game object template - root must be a GameObject: " + chosenTemplate);
            return null;
        }

        var newComponent = template.Component?.Duplicate() as REComponent;
        if (newComponent == null) {
            GD.PrintErr("Component template is empty: " + chosenTemplate);
            return null;
        }
        if (string.IsNullOrEmpty(newComponent.Classname)) {
            target.AddComponent(newComponent = new REComponentPlaceholder());
            return newComponent;
        }

        template.ApplyProperties(newComponent, ".");

        target.AddOrReplaceComponent(newComponent.Classname, newComponent);
        target.NotifyPropertyListChanged();
        target.MarkSceneChangedIfChildOfActiveScene();
        GD.Print("Added component " + newComponent.Classname);
        return newComponent;
    }
}

public enum ObjectTemplateType
{
    GameObject,
    Component,
}