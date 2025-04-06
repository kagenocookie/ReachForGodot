namespace ReaGE;

using Godot;

[GlobalClass, Tool, ResourceHolder("tml", SupportedFileFormats.Timeline)]
public partial class TimelineResource : REResource
{
    public TimelineResource() : base(SupportedFileFormats.Timeline)
    {
    }
}
