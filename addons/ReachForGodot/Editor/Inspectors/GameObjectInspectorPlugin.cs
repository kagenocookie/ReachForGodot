#if TOOLS
using System.Threading.Tasks;
using Godot;

namespace RGE;

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
        // return @object is REGameObject;
        return false;
    }

    public override bool _ParseProperty(GodotObject @object, Variant.Type type, string name, PropertyHint hintType, string hintString, PropertyUsageFlags usageFlags, bool wide)
    {
        if (@object is REGameObject target) {
            if (name == nameof(REGameObject.Components)) {
                CreateComponentsUI(target);
                return true;
            }
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

        // inspector = EditorInspector.InstantiatePropertyEditor(
        //     placeholder,
        //     Variant.Type.Array,
        //     "Components",
        //     PropertyHint.TypeString,
        //     $"{(int)Variant.Type.Object}/{(int)PropertyHint.ResourceType}:{nameof(REComponent)}",
        //     (uint)(PropertyUsageFlags.ScriptVariable|PropertyUsageFlags.Default|PropertyUsageFlags.ReadOnly),
        //     true);
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
        // Task.Delay(100).ContinueWith((_) => {
        //     inspector.NotifyPropertyListChanged();
        //     placeholder.NotifyPropertyListChanged();
        //     inspector.SetObjectAndProperty(placeholder, nameof(GameobjectEditorPlaceholder.Component));
        //     GD.Print("WOOOOOOOOO");
        // });
            inspector.NotifyPropertyListChanged();
            placeholder.NotifyPropertyListChanged();

        filter.TextChanged += (text) => {
            // Debug.Assert(target != null);
            // Debug.Assert(target.Components != null);
            // Debug.Assert(placeholder != null);
            // placeholder.Components = new Godot.Collections.Array<REComponent>(
            //     string.IsNullOrEmpty(text)
            //     ? target.Components
            //     : target.Components.Where(x => x.Classname?.Contains(text, StringComparison.OrdinalIgnoreCase) == true));
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