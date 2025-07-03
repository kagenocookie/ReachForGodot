namespace ReaGE;

using Godot;
using ReeLib;
using ReeLib.Aimp;

// TODO: this resource class could probably store all the different ai** formats
[GlobalClass, Tool, ResourceHolder("aimap", KnownFileFormats.AIMap)]
public partial class AiMapResource : REResource
{
    [Export] public MapType mapType;
    [Export] public SectionType sectionType;
    [Export] public string? name;
    [Export] public string? hash;
    [Export] public MapLayerInfo[]? Layers;
    [Export] public Godot.Collections.Array<REObject>? Userdata;
    [Export] public ulong uriHash;
    [Export] public float DefaultAgentRadius;
    // TODO embedded maps

    public AiMapResource() : base(KnownFileFormats.AIMap)
    {
    }
}
