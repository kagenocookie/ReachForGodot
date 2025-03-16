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
        return @object is UserdataResource or RcolRootNode;
    }

    public override void _ParseBegin(GodotObject @object)
    {
        if (@object is IRszContainer res and (RcolResource or UserdataResource)) {
            CreateUI(res);
        }
        base._ParseBegin(@object);
    }

    private void CreateUI(IRszContainer res)
    {
        var container = new VBoxContainer();
        var emptyLabel = new Label() { Text = "Object is uninitialized. Make sure a source asset is defined and press 'Reimport file'." };

        emptyLabel.Visible = res.IsEmpty && (res is not REResourceProxy proxy || proxy.ImportedResource == null);
        var reimportButton = new Button() { Text = "Reimport file", SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        reimportButton.Pressed += () => {
            if (res.Asset?.IsEmpty != false) {
                GD.PrintErr("User file asset source path is unset!");
                return;
            }
            if (res is REResource reres) {
                Reimport(reres);
                emptyLabel.Visible = res.IsEmpty && (res is not REResourceProxy proxy || proxy.ImportedResource == null);
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
        serializationFixer.Register((GodotObject)res, root);
    }

    private void Reimport(REResource resource)
    {
        var conv = new GodotRszImporter(ReachForGodot.GetAssetConfig(resource.Game), GodotRszImporter.importTreeChanges);
        if (resource is UserdataResource ur) {
            conv.GenerateUserdata(ur);
        } else if (resource is RcolResource rcol) {
            conv.GenerateRcol(rcol);
        }
        resource.NotifyPropertyListChanged();
    }
}
#endif