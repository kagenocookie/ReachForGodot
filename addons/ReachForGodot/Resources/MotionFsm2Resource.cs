namespace ReaGE;

using Godot;
using ReeLib;

[GlobalClass, Tool, ResourceHolder("motfsm2", KnownFileFormats.MotionFsm2)]
public partial class MotionFsm2Resource : REResource
{
    public MotionFsm2Resource() : base(KnownFileFormats.MotionFsm2)
    {
    }
}
