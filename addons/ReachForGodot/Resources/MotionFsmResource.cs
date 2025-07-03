namespace ReaGE;

using Godot;
using ReeLib;

[GlobalClass, Tool, ResourceHolder("motfsm", KnownFileFormats.MotionFsm)]
public partial class MotionFsmResource : REResource
{
    public MotionFsmResource() : base(KnownFileFormats.MotionFsm)
    {
    }
}
