#if TOOLS
using System.Text.RegularExpressions;
using Godot;

namespace ReaGE;

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

    [GeneratedRegex("[^a-zA-Z0-9-_/\\.]")]
    private static partial Regex FilepathRegex();

    public override bool _ParseProperty(GodotObject @object, Variant.Type type, string name, PropertyHint hintType, string hintString, PropertyUsageFlags usageFlags, bool wide)
    {
        if (type == Variant.Type.Object && name == "Asset" && @object.Get(name).As<AssetReference>() is AssetReference asset) {
            var propertyEdit = new AssetReferenceProperty();
            AddPropertyEditor("Asset", propertyEdit);


            pluginSerializationFixer.Register(asset, propertyEdit);
            return true;
        }
        return base._ParseProperty(@object, type, name, hintType, hintString, usageFlags, wide);
    }
}
#endif