namespace ReaGE;

using Godot;

[GlobalClass, Tool, ResourceHolder("motfsm", SupportedFileFormats.MotionFsm)]
public partial class MotionFsmResource : REResource
{
    public MotionFsmResource() : base(SupportedFileFormats.MotionFsm)
    {
    }
}
