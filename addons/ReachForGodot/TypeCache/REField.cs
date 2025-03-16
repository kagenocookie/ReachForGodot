namespace ReaGE;
using Godot;
using RszTool;

public class REField
{
    public required RszField RszField { get; init; }
    public required int FieldIndex { get; init; }
    public Variant.Type VariantType { get; set; }
    public PropertyHint Hint { get; set; }
    public string? HintString { get; set; }
    public string? ElementType { get; set; }
    public RESupportedFileFormats ResourceType {
        set => HintString = PathUtils.GetResourceTypeFromFormat(value).Name;
    }

    public string SerializedName => RszField.name;

    public void MarkAsResource(string resourceTypeName)
    {
        RszField.type = RszFieldType.Resource;
        RszField.IsTypeInferred = true;
        VariantType = Variant.Type.Object;
        Hint = PropertyHint.ResourceType;
        HintString = resourceTypeName;
    }

    public void MarkAsType(RszFieldType rszType, string godotResourceTypeName)
    {
        RszField.type = rszType;
        RszField.IsTypeInferred = true;
        VariantType = Variant.Type.Object;
        Hint = PropertyHint.ResourceType;
        HintString = godotResourceTypeName;
    }
}
