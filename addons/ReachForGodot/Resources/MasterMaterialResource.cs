namespace ReaGE;

using Godot;

[GlobalClass, Tool, ResourceHolder("mmtr", RESupportedFileFormats.MasterMaterial)]
public partial class MasterMaterialResource : REResource
{
    public MasterMaterialResource() : base(RESupportedFileFormats.MasterMaterial)
    {
    }
}
