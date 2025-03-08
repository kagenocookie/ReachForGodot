#if TOOLS
using Godot;

namespace ReaGE;

public class PluginSerializationFixer : ISerializationListener
{
    private System.Collections.Generic.Dictionary<GodotObject, Control> nodes = new();

    public void OnAfterDeserialize()
    {
    }

    public void OnBeforeSerialize()
    {
        // workaround for editor assembly unload, need to yeet anything that has closure reference to objects (buttons)
        foreach (var (node, ui) in nodes) {
            if (GodotObject.IsInstanceValid(ui) && GodotObject.IsInstanceValid(node)) {
                ui.GetParent().RemoveChild(ui);
                ui.Free();
            }
        }
        nodes.Clear();
    }

    public void Register(GodotObject obj, Control ctrl) => nodes[obj] = ctrl;
}
#endif