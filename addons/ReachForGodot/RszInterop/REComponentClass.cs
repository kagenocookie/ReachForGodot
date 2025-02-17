namespace RFG;

[System.AttributeUsage(System.AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
public sealed class REComponentClassAttribute : System.Attribute
{
    public string Classname { get; }
    public string[] SupportedGames { get; }

    public REComponentClassAttribute(string classname, params string[] supportedGames)
    {
        Classname = classname;
        SupportedGames = supportedGames;
    }
}