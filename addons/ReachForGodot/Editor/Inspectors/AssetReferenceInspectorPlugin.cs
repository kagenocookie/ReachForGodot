#if TOOLS
using System.Text.RegularExpressions;
using Godot;
using Godot.Collections;

namespace RGE;

public partial class AssetReferenceInspectorPlugin : EditorInspectorPlugin, ISerializationListener
{
    private static PluginSerializationFixer pluginSerializationFixer = new();

    public void OnAfterDeserialize() { }
    public void OnBeforeSerialize() => pluginSerializationFixer.OnBeforeSerialize();

    public override bool _CanHandle(GodotObject @object)
    {
        return @object is REResource or IRszContainerNode;
    }

    private PackedScene? inspectorScene;

    [GeneratedRegex("[^a-zA-Z0-9-_/.]")]
    private static partial Regex FilepathRegex();

    public override bool _ParseProperty(GodotObject @object, Variant.Type type, string name, PropertyHint hintType, string hintString, PropertyUsageFlags usageFlags, bool wide)
    {
        if (type == Variant.Type.Object && name == "Asset" && @object.Get(name).As<AssetReference>() is AssetReference asset) {
            inspectorScene ??= ResourceLoader.Load<PackedScene>("res://addons/ReachForGodot/Editor/Inspectors/AssetReferenceInspectorPlugin.tscn");

            var container = inspectorScene.Instantiate<Control>();
            container.RequireChildByTypeRecursive<Button>().Pressed += () => asset.OpenSourceFile(@object.Get("Game").As<SupportedGame>());
            var text = container.RequireChildByTypeRecursive<TextEdit>();
            text.Text = asset.AssetFilename;
            text.TextChanged += () => {
                var fixedText = FilepathRegex().Replace(text.Text, "");
                if (fixedText != text.Text) {
                    text.Text = fixedText;
                }
                asset.AssetFilename = text.Text;
            };
            AddCustomControl(container);
            pluginSerializationFixer.Register(asset, container);
            return true;
        }
        return base._ParseProperty(@object, type, name, hintType, hintString, usageFlags, wide);
    }

    private Button CreateButton(string label, Action action)
    {
        var btn = new Button() { Text = label };
        btn.Pressed += action;
        return btn;
    }

    // private void CreateUI(GodotObject obj)
    // {
    //     pluginSerializationFixer.Register(obj, container);
    // }
}
#endif