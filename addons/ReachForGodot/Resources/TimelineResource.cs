namespace ReaGE;

using Godot;
using ReeLib;

[GlobalClass, Tool, ResourceHolder("tml", KnownFileFormats.Timeline)]
public partial class TimelineResource : REResource
{
    public TimelineResource() : base(KnownFileFormats.Timeline)
    {
    }
}
