namespace REFDumpFormatter;

public class ObjectDef
{
    // public string? address { get; set; }
    // public string? crc { get; set; }
    // public string? fqn { get; set; }
    // public string? flags { get; set; }
    public string? parent { get; set; }
    // public int id { get; set; }
    // public bool is_generic_type { get; set; }
    // public bool is_generic_type_definition { get; set; }
    // public Dictionary<string, MethodDef>? methods { get; set; }
    public Dictionary<string, FieldDef>? fields { get; set; }
    // public bool IsAbstract => flags?.Contains("Abstract") == true;
    // public bool IsNative => flags?.Contains("NativeType") == true;
}
