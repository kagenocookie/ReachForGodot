namespace ReaGE.EFX;

using Godot;
using Godot.Collections;
using RszTool.Efx;

[GlobalClass, Tool, Icon("res://addons/ReachForGodot/icons/efx_attr.png")]
public partial class EfxAttributeNode : Node3D
{
    [Export] public Vector3I NodePosition;
    [Export] public EfxAttributeType Type { get; set; }
    [Export] public EfxObject? Data { get; set; }

    public override void _ValidateProperty(Dictionary property)
    {
        if (property["name"].AsStringName() == PropertyName.Type) {
            property["usage"] = (int)(PropertyUsageFlags.ReadOnly|PropertyUsageFlags.Default);
        }
        base._ValidateProperty(property);
    }
}
