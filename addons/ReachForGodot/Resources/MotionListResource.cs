namespace ReaGE;

using Godot;
using ReeLib;

[GlobalClass, Tool, ResourceHolder("motlist", KnownFileFormats.MotionList)]
public partial class MotionListResource : REResource
{
    public MotionListResource() : base(KnownFileFormats.MotionList)
    {
    }
}
