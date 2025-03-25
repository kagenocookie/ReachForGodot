namespace ReaGE;

using Godot;

[GlobalClass, Tool, ResourceHolder("mdf2", RESupportedFileFormats.Material)]
public partial class MaterialResource : REResource
{
    public MaterialResource() : base(RESupportedFileFormats.Material)
    {
    }
}
