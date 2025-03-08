namespace ReaGE;

using System.Threading.Tasks;
using Godot;

/// <summary>
/// Proxy placeholder resource for resources that have a non-REResource compatible representation in engine (e.g. meshes)
/// </summary>
[GlobalClass, Tool]
public partial class REResourceProxy : REResource
{
    [Export] public Resource? ImportedResource { get; set; }

    [ExportToolButton("Re-Import asset")]
    private Callable ForceReimport => Callable.From(() => { Import(true); });

    public Task<Resource?> Import(bool forceReload)
    {
        if (!forceReload && ImportedResource != null) {
            return Task.FromResult<Resource?>(ImportedResource);
        }

        ImportedResource = null;
        return Import();
    }
    protected virtual Task<Resource?> Import() => Task.FromResult((Resource?)null);
}
