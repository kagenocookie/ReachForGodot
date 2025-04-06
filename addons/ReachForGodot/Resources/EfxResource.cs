namespace ReaGE;

using Godot;

[GlobalClass, Tool, ResourceHolder("efx", SupportedFileFormats.Efx)]
public partial class EfxResource : REResource
{
    public EfxResource() : base(SupportedFileFormats.Efx)
    {
    }
}
