namespace ReaGE.EFX;

using System;
using Godot;
using RszTool.Efx;

[GlobalClass, Tool, Icon("res://addons/ReachForGodot/icons/efx_action.png")]
public partial class EfxActionNode : Node3D
{
    [Export] public string? OriginalName;
    [Export] public int index;

    public IEnumerable<EfxAttributeNode> Attributes => this.FindChildrenByType<EfxAttributeNode>();

    public void RegenerateNodeName()
    {
        var baseName = OriginalName ?? ((this is EfxNode ? "Node_" : "Action_") + GetIndex().ToString("00"));
        // attempt translate?
        var typeAttr = Attributes.FirstOrDefault(attr => attr.Data?.TypeInfo.Info.Name.StartsWith("Type") == true);
        if (typeAttr?.Data != null) {
            Name = baseName + "__" + typeAttr.Data.TypeInfo.Info.Name.Replace("Type", "");
            return;
        }

        typeAttr = Attributes.FirstOrDefault(attr => attr.Data?.TypeInfo.Info.Name.StartsWith("Unknown") == true && attr.Data.TypeInfo.Info.Name.Contains("_Type"));
        if (typeAttr?.Data != null) {
            var typename = typeAttr.Data.TypeInfo.Info.Name.Substring(typeAttr.Data.TypeInfo.Info.Name.IndexOf("_Type") + "_Type".Length);
            Name = baseName + "__" + typename;
            return;
        }

        Name = baseName;
    }
}
