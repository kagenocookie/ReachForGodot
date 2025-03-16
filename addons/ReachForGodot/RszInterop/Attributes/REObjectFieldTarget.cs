namespace ReaGE;

/// <summary>
/// Attribute to annotate static REObjectFieldAccessor fields to override the class they override.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
public sealed class REObjectFieldTargetAttribute : System.Attribute
{
    public string Classname { get; }
    public SupportedGame[] SupportedGames { get; }

    public REObjectFieldTargetAttribute(string classname, params SupportedGame[] supportedGames)
    {
        Classname = classname;
        SupportedGames = supportedGames;
    }
}