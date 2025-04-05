namespace ReaGE;

using Godot;

[GlobalClass, Tool, ResourceHolder("motlist", SupportedFileFormats.MotionList)]
public partial class MotionListResource : REResource
{
    public MotionListResource() : base(SupportedFileFormats.MotionList)
    {
    }
}
