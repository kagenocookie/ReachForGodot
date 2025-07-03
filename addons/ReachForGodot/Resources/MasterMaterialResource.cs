namespace ReaGE;

using Godot;
using ReeLib;

[GlobalClass, Tool, ResourceHolder("mmtr", KnownFileFormats.MasterMaterial)]
public partial class MasterMaterialResource : REResource
{
    public MasterMaterialResource() : base(KnownFileFormats.MasterMaterial)
    {
    }
}
