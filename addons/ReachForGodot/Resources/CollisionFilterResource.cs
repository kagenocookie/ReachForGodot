namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, Tool, ResourceHolder("cfil", SupportedFileFormats.CollisionFilter)]
public partial class CollisionFilterResource : REResource, IImportableAsset
{
    [Export] public string? Uuid;
    [Export] public string[]? CollisionGuids;

    public CollisionFilterResource() : base(SupportedFileFormats.CollisionFilter)
    {
    }

    public bool IsEmpty => string.IsNullOrEmpty(Uuid) || CollisionGuids == null;
}
