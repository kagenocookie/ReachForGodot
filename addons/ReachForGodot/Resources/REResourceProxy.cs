namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using ReeLib;

/// <summary>
/// Proxy placeholder resource for resources that have a non-REResource compatible representation in engine (e.g. meshes)
/// </summary>
[GlobalClass, Tool]
public partial class REResourceProxy : REResource, IImportableAsset
{
    [Export] public Resource? ImportedResource { get; set; }
    public bool IsEmpty => ImportedResource == null;

    public REResourceProxy() { }
    protected REResourceProxy(KnownFileFormats format) => ResourceType = format;

    public async Task<Resource?> Import(bool forceReload)
    {
        if (!forceReload && ImportedResource != null) {
            return ImportedResource;
        }

        ImportedResource = null;
        return ImportedResource = await Import();
    }

    public virtual Resource? GetOrCreatePlaceholder(GodotImportOptions options) => ImportedResource;

    protected virtual Task<Resource?> Import() => Task.FromResult((Resource?)null);
}
