#if TOOLS
using System.Text.RegularExpressions;
using Godot;

namespace ReaGE;

public partial class ResourceInspectorPlugin : EditorInspectorPlugin, ISerializationListener
{
    private static PluginSerializationFixer serializationFixer = new();

    public void OnAfterDeserialize() { }
    public void OnBeforeSerialize() => serializationFixer.OnBeforeSerialize();

    public override bool _CanHandle(GodotObject @object)
    {
        return @object is UserdataResource;
    }

    public override void _ParseBegin(GodotObject @object)
    {
        if (@object is UserdataResource res) {
            CreateUI(res);
        }
        base._ParseBegin(@object);
    }

    private void CreateUI(UserdataResource res)
    {
        var container = new VBoxContainer();
        var emptyLabel = new Label() { Text = "Object is uninitialized. Make sure a source asset is defined and press 'Reimport file'." };

        emptyLabel.Visible = res.IsEmpty;
        var reimportButton = new Button() { Text = "Reimport file", SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        reimportButton.Pressed += () => {
            if (res is UserdataResource ur) {
                if (ur.Asset?.IsEmpty == false) {
                    ur.Reimport();
                    emptyLabel.Visible = res.IsEmpty;
                } else {
                    GD.PrintErr("User file asset source path is unset!");
                }
            }
        };
        var hbox = new HBoxContainer();
        hbox.AddChild(reimportButton);
        hbox.AddChild(new Label() { Text = "Note that any local changes will be lost on reimport." });

        container.AddChild(emptyLabel);
        container.AddChild(hbox);

        var root = new MarginContainer();
        root.AddThemeConstantOverride("margin_left", 8);
        root.AddThemeConstantOverride("margin_bottom", 4);
        root.AddChild(container);
        AddCustomControl(root);
        serializationFixer.Register(res, root);
    }
}
#endif