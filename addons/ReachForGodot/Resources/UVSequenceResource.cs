namespace ReaGE;

using Godot;

[GlobalClass, Tool, ResourceHolder("uvs", SupportedFileFormats.UVSequence)]
public partial class UVSequenceResource : REResource
{
    public UVSequenceResource() : base(SupportedFileFormats.UVSequence)
    {
    }
}
