namespace RGE;

[System.AttributeUsage(System.AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
public sealed class REComponentClassAttribute : System.Attribute
{
    public string Classname { get; }
    public SupportedGame[] SupportedGames { get; }

    public REComponentClassAttribute(string classname, params SupportedGame[] supportedGames)
    {
        Classname = classname;
        SupportedGames = supportedGames;
    }
}