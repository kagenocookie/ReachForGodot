namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, Tool, ResourceHolder("cfil", RESupportedFileFormats.CollisionFilter)]
public partial class CollisionFilterResource : REResource, IImportableAsset
{
    [Export] public string? Uuid;
    [Export] public string[]? CollisionGuids;

    public CollisionFilterResource() : base(RESupportedFileFormats.CollisionFilter)
    {
    }

    public bool IsEmpty => string.IsNullOrEmpty(Uuid) || CollisionGuids == null;

    public Task<bool> Import(string resolvedFilepath, GodotRszImporter importer)
    {
        using var file = new CfilFile(new FileHandler(resolvedFilepath));
        file.Read();
        Uuid = file.myGuid.ToString();
        CollisionGuids = file.Guids?.Select(g => g.ToString()).ToArray();
        return Task.FromResult(true);
    }
}

