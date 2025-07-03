namespace ReaGE;

using ReeLib;

[System.AttributeUsage(System.AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class ResourceHolderAttribute : System.Attribute
{
    public string Extension { get; }
    public KnownFileFormats Format { get; }

    public ResourceHolderAttribute(string extension, KnownFileFormats format)
    {
        Extension = extension;
        Format = format;
    }
}