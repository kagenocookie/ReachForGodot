namespace ReaGE;

[System.AttributeUsage(System.AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class ResourceHolderAttribute : System.Attribute
{
    public string Extension { get; }
    public RESupportedFileFormats Format { get; }

    public ResourceHolderAttribute(string extension, RESupportedFileFormats format)
    {
        Extension = extension;
        Format = format;
    }
}