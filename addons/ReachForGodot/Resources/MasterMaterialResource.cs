namespace ReaGE;

using Godot;

[GlobalClass, Tool, ResourceHolder("mmtr", SupportedFileFormats.MasterMaterial)]
public partial class MasterMaterialResource : REResource
{
    public MasterMaterialResource() : base(SupportedFileFormats.MasterMaterial)
    {
    }
}
