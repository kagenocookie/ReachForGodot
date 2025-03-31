namespace ReaGE;

[System.AttributeUsage(System.AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class FieldAccessorProviderAttribute : System.Attribute
{
    public string? Classname { get; }
    public SupportedGame[] SupportedGames { get; }

    public FieldAccessorProviderAttribute(string? classname = null, params SupportedGame[] supportedGames)
    {
        Classname = classname;
        SupportedGames = supportedGames;
    }
}