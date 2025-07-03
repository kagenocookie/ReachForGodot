namespace ReaGE;

using Godot;
using ReeLib;

[GlobalClass, Tool, ResourceHolder("gui", KnownFileFormats.GUI)]
public partial class GuiResource : REResource
{
    public GuiResource() : base(KnownFileFormats.GUI)
    {
    }
}
