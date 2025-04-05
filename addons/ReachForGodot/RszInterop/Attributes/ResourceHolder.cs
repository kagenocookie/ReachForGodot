namespace ReaGE;

[System.AttributeUsage(System.AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class ResourceHolderAttribute : System.Attribute
{
    public string Extension { get; }
    public SupportedFileFormats Format { get; }

    public ResourceHolderAttribute(string extension, SupportedFileFormats format)
    {
        Extension = extension;
        Format = format;
    }
}