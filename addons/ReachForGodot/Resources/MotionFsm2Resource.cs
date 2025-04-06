namespace ReaGE;

using Godot;

[GlobalClass, Tool, ResourceHolder("motfsm2", SupportedFileFormats.MotionFsm2)]
public partial class MotionFsm2Resource : REResource
{
    public MotionFsm2Resource() : base(SupportedFileFormats.MotionFsm2)
    {
    }
}
