#if TOOLS
using System.Threading.Tasks;
using Godot;

namespace ReaGE;

public partial class GameObjectInspectorPlugin : EditorInspectorPlugin, ISerializationListener
{
    private static PluginSerializationFixer pluginSerializationFixer = new();

    public void OnAfterDeserialize() { }
    public void OnBeforeSerialize()
    {
        pluginSerializationFixer.OnBeforeSerialize();
        placeholder?.Free();
        placeholder = null;
        inspector = null;
    }

    private PackedScene? inspectorScene;
    private GameobjectEditorPlaceholder? placeholder;
    private EditorProperty? inspector;

    public override bool _CanHandle(GodotObject @object)
    {
        return @object is REGameObject;
    }

    public override void _ParseBegin(GodotObject @object)
    {
        if (@object is REGameObject gameobj) {
            AddButtons(gameobj);
        }
        base._ParseBegin(@object);
    }

    private void AddButtons(REGameObject gameobj)
    {
        if (gameobj.Owner == null) return;

        var container = new MarginContainer();
        container.AddThemeConstantOverride("margin_left", 2);
        container.AddThemeConstantOverride("margin_bottom", 6);
        var container2 = new HBoxContainer();
        var btn = new Button() { Text = "Clone this GameObject" };
        btn.Pressed += () => {
            var action = new GameObjectCloneAction(gameobj);
            action.Trigger();
            EditorInterface.Singleton.EditNode(action.Clone);
        };
        container2.AddChild(btn);
        container.AddChild(container2);

        AddCustomControl(container);
        pluginSerializationFixer.Register(gameobj, container);
    }

    private void DoClone(REGameObject source)
    {
        var clone = source.Clone();
        source.GetParent().AddUniqueNamedChild(clone);
        source.GetParent().MoveChild(clone, source.GetIndex() + 1);
        SetChildrenOwner(clone, source.Owner);
    }

    private static void SetChildrenOwner(Node node, Node owner)
    {
        foreach (var child in node.GetChildren()) {
            child.Owner = owner;
            if (string.IsNullOrEmpty(child.SceneFilePath)) {
                SetChildrenOwner(child, owner);
            }
        }
    }

    public override bool _ParseProperty(GodotObject @object, Variant.Type type, string name, PropertyHint hintType, string hintString, PropertyUsageFlags usageFlags, bool wide)
    {
        if (@object is REGameObject target) {
            // if (name == nameof(REGameObject.Components)) {
            //     CreateComponentsUI(target);
            //     return true;
            // }
        }
        return base._ParseProperty(@object, type, name, hintType, hintString, usageFlags, wide);
    }

    private void CreateComponentsUI(REGameObject target)
    {
        inspectorScene ??= ResourceLoader.Load<PackedScene>("res://addons/ReachForGodot/Editor/Inspectors/GameObjectInspectorPlugin.tscn");
        var root = inspectorScene.Instantiate<Control>();

        var filter = root.GetNode<LineEdit>("%Filter");

        if (placeholder != null) {
            placeholder.QueueFree();
        }
        placeholder = new();


        placeholder.Components = new Godot.Collections.Array<REComponent>(target.Components);

        placeholder.Component = target.Components[0];
        inspector = EditorInspector.InstantiatePropertyEditor(
            target,
            Variant.Type.Object,
            nameof(GameobjectEditorPlaceholder.Component),
            PropertyHint.ResourceType,
            $"{nameof(REComponent)}",
            (uint)(PropertyUsageFlags.ScriptVariable|PropertyUsageFlags.Default),
            true);

        AddCustomControl(root);
        AddCustomControl(inspector);
        placeholder.Component = target.Components[0];
        inspector.SetObjectAndProperty(placeholder, nameof(GameobjectEditorPlaceholder.Component));
        placeholder.Component = target.Components[0];
            inspector.NotifyPropertyListChanged();
            placeholder.NotifyPropertyListChanged();

        filter.TextChanged += (text) => {
            inspector.SetObjectAndProperty(placeholder, nameof(GameobjectEditorPlaceholder.Component));
            inspector.UpdateProperty();
        };
        inspector.PropertyChanged += (property, updated, field, changing) => {
            GD.PrintErr("Changing components list currently not supported.");
            var newlist = updated.AsGodotArray<REComponent>();
            GD.Print("Placeholder comp count: " + placeholder.Components.Count);
            GD.Print("Real comp count: " + target.Components.Count);
        };
        inspector.UpdateProperty();

        pluginSerializationFixer.Register((GodotObject)target, root);
        pluginSerializationFixer.Register((GodotObject)placeholder, inspector);
    }
}
#endif