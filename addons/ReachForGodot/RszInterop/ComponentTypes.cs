namespace RGE;

using System;
using System.Reflection;
using Godot;
using RszTool;

public static class ComponentTypes
{
    private static readonly Type[] SetupMethodSignature = [typeof(IRszContainerNode), typeof(REGameObject), typeof(RszInstance)];

    public static void Init()
    {
        var componentTypes = typeof(ComponentTypes).Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<REComponentClassAttribute>() != null && !t.IsAbstract);

        foreach (var type in componentTypes) {
            if (!type.IsAssignableTo(typeof(REComponent)) || type.IsAbstract) {
                GD.PrintErr($"Invalid REComponentClass annotated type {type.FullName}.\nMust be a non-abstract REComponent node.");
                continue;
            }

            var attr = type.GetCustomAttribute<REComponentClassAttribute>()!;
            RszGodotConverter.DefineComponentFactory(attr.Classname, (a, b, c) => {
                var node = (REComponent)Activator.CreateInstance(type)!;
                node.Name = attr.Classname;
                node.Setup(a, b, c);
                return node;
            }, attr.SupportedGames);
        }
    }
}