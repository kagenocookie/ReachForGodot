namespace ReaGE;

using Godot;

[GlobalClass, Tool, ResourceHolder("motlist", RESupportedFileFormats.MotionList)]
public partial class MotionListResource : REResource
{
    public MotionListResource() : base(RESupportedFileFormats.MotionList)
    {
    }
}
