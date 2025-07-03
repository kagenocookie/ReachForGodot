namespace ReaGE;

using Godot;
using ReeLib;

[GlobalClass, Tool, ResourceHolder("hf", KnownFileFormats.HeightField)]
public partial class HeightFieldResource : REResource
{
    public HeightFieldResource() : base(KnownFileFormats.HeightField)
    {
    }
}
