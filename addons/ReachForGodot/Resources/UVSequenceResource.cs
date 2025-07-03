namespace ReaGE;

using Godot;
using ReeLib;

[GlobalClass, Tool, ResourceHolder("uvs", KnownFileFormats.UVSequence)]
public partial class UVSequenceResource : REResource
{
    public UVSequenceResource() : base(KnownFileFormats.UVSequence)
    {
    }
}
