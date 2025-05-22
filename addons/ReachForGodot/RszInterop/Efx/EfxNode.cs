namespace ReaGE.EFX;

using System;
using Godot;
using RszTool.Efx;

[GlobalClass, Tool, Icon("res://addons/ReachForGodot/icons/efx_entry.png")]
public partial class EfxNode : EfxActionNode
{
    [Export] public EfxEntryEnum Assignment;
    [Export] public string[]? Groups;
}
