namespace ReaGE;

using Godot;

// TODO: this resource class could probably store all the different ai** formats
[GlobalClass, Tool, ResourceHolder("aimap", SupportedFileFormats.AiMap)]
public partial class AiMapResource : REResource
{
    public AiMapResource() : base(SupportedFileFormats.AiMap)
    {
    }
}
