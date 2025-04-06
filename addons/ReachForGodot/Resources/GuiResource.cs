namespace ReaGE;

using Godot;

[GlobalClass, Tool, ResourceHolder("gui", SupportedFileFormats.Gui)]
public partial class GuiResource : REResource
{
    public GuiResource() : base(SupportedFileFormats.Gui)
    {
    }
}
