namespace ReaGE;

using System;
using Godot;
using Godot.Collections;
using ReaGE;

[GlobalClass, Tool]
public partial class ComponentTemplate : Node
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