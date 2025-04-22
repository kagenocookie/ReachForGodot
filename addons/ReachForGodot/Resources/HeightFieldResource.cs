namespace ReaGE;

using Godot;

[GlobalClass, Tool, ResourceHolder("hf", SupportedFileFormats.HeightField)]
public partial class HeightFieldResource : REResource
{
    public HeightFieldResource() : base(SupportedFileFormats.HeightField)
    {
    }
}
