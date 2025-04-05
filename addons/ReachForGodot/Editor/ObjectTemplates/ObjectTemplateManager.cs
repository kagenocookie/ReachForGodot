namespace ReaGE;

using System;
using Godot;

public static class ObjectTemplateManager
{
    private static string GetBaseTemplateFolder(ObjectTemplateType type, SupportedGame game)
    {
        var templateFolder = type switch {
            ObjectTemplateType.GameObject => "GameObject",
            ObjectTemplateType.Component => "Component",
            _ => "GameObjects",
        };
        return $"res://addons/ReachForGodot/game_config/{GamePaths.GetShortName(game)}/templates/{templateFolder}";
    }

    public static string[] GetAvailableTemplates(ObjectTemplateType type, SupportedGame game)
    {
        var folder = GetBaseTemplateFolder(type, game);

        var fa = DirAccess.Open(folder);
        if (fa == null) {
            var err = DirAccess.GetOpenError();
            return Array.Empty<string>();
        }
        return fa.GetFiles().Select(filename => Path.Combine(folder, filename)).ToArray();
    }

    public static void InstantiateGameobject(string chosenTemplate, Node parent, Node owner)
    {
        var sourceInstance = ResourceLoader.Load<PackedScene>(chosenTemplate).Instantiate<Node>(PackedScene.GenEditState.Instance);
        if (sourceInstance is not GameObject sourceGameObject) {
            GD.PrintErr("Invalid game object template - root must be a GameObject: " + chosenTemplate);
            return;
        }

        var clone = sourceGameObject.Clone();
        parent.AddUniqueNamedChild(clone);
        clone.SetRecursiveOwner(owner, clone);
    }

    public static void InstantiateComponent(string chosenTemplate, GameObject target)
    {
        var sourceInstance = ResourceLoader.Load<PackedScene>(chosenTemplate).Instantiate<Node>(PackedScene.GenEditState.Instance);
        if (sourceInstance is not ComponentTemplate template) {
            GD.PrintErr("Invalid game object template - root must be a GameObject: " + chosenTemplate);
            return;
        }

        var newComponent = template.Component?.Duplicate() as REComponent;
        if (newComponent == null) {
            GD.PrintErr("Component template is empty: " + chosenTemplate);
            return;
        }
        if (string.IsNullOrEmpty(newComponent.Classname)) {
            target.AddComponent(new REComponentPlaceholder());
            return;
        }

        target.AddOrReplaceComponent(newComponent.Classname, newComponent);
        target.NotifyPropertyListChanged();
        GD.Print("Added component " + newComponent.Classname);
    }
}

public enum ObjectTemplateType
{
    GameObject,
    Component,
}