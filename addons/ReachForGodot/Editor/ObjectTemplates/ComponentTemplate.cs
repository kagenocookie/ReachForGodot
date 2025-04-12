namespace ReaGE;

using Godot;
using Godot.Collections;

[GlobalClass, Tool]
public partial class ComponentTemplate : ObjectTemplateRoot
{
    [Export] public REComponent? Component { get; set; }

    public override Array<Dictionary> _GetPropertyList()
    {
        if (Component is REComponentPlaceholder placeholder) {
            placeholder.SetBaseClass("via.Component");
        }

        return base._GetPropertyList();
    }
}