namespace ReaGE;

using Godot;
using Godot.Collections;

[GlobalClass, Tool]
public partial class MaterialResource : Resource
{
    [Export] public SupportedGame Game { get; set; }
    [Export] public string? MaterialName { get; set; }
    [Export] public Godot.Collections.Dictionary<string, TextureResource>? Textures { get; set; }
    [Export] public MaterialPropertyList? Params { get; set; }
    [Export] public int ShaderType { get; set; }
    // TODO: verify if there's different flags / flag structures for different games?
    [Export] public MaterialFlags1 AlphaFlags { get; set; }
    [Export] public MaterialFlags2 AlphaFlags2 { get; set; }
    [Export] public int TesselationFactor { get; set; }
    [Export] public int PhongFactor { get; set; }
    [Export] public MasterMaterialResource? MasterMaterial { get; set; }
    [Export] public Godot.Collections.Dictionary<string, GpuBufferResource>? GpuBuffers { get; set; }

    public override void _ValidateProperty(Dictionary property)
    {
        if (property["name"].AsStringName() == PropertyName.ShaderType) {
            property["hint"] = (int)PropertyHint.Enum;
            property["hint_string"] = TypeCache.GetEnumHintString(Game, "via.render.MaterialShadingType");
        }
        base._ValidateProperty(property);
    }
}

[Flags]
public enum MaterialFlags1 {
	BaseTwoSideEnable = (1 << 0),
	BaseAlphaTestEnable = (1 << 1),
	ShadowCastDisable = (1 << 2),
	VertexShaderUsed = (1 << 3),
	EmissiveUsed = (1 << 4),
	TessellationEnable = (1 << 5),
	EnableIgnoreDepth = (1 << 6),
	AlphaMaskUsed = (1 << 7),
	ForcedTwoSideEnable = (1 << 8),
	TwoSideEnable = (1 << 9),
}

[Flags]
public enum MaterialFlags2 {
	RoughTransparentEnable = (1 << 0),
	ForcedAlphaTestEnable = (1 << 1),
	AlphaTestEnable = (1 << 2),
	SSSProfileUsed = (1 << 3),
	EnableStencilPriority = (1 << 4),
	RequireDualQuaternion = (1 << 5),
	PixelDepthOffsetUsed = (1 << 6),
	NoRayTracing = (1 << 7),
}
