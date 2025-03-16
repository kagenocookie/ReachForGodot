namespace ReaGE;

[System.AttributeUsage(System.AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class REObjectClassAttribute : System.Attribute
{
    public string Classname { get; }
    public SupportedGame[] SupportedGames { get; }

    public REObjectClassAttribute(string classname, params SupportedGame[] supportedGames)
    {
        Classname = classname;
        SupportedGames = supportedGames;
    }
}