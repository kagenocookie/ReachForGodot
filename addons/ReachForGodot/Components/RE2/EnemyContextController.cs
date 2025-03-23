namespace ReaGE.Components.RE2;

using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, Tool, REComponentClass("app.ropeway.EnemyContextController", SupportedGame.ResidentEvil2RT)]
public partial class EnemyContextController : REComponent
{
    public EnemyContextController() { }
    public EnemyContextController(SupportedGame game, string classname) : base(game, classname) {}

    private static readonly REFieldAccessor InitialKind = new REFieldAccessor("InitialKind").WithConditions("InitialKind");

    private const string NodeName = "__EnemyPreview";

    private int ID => TryGetFieldValue(InitialKind, out var val) ? val.AsInt32() : -1;

    public string? GetMeshFilepath()
    {
        var id = ID;
        if (id > 0) {
            var label = TypeCache.GetEnumLabel(Game, "app.ropeway.EnemyDefine.KindID", id);
            var config = ReachForGodot.GetAssetConfig(Game);
            var resolved = PathUtils.FindSourceFilePath($"SectionRoot/Character/Enemy/{label}/Body/Body00/{label}_body00.mesh", config)
                        ?? PathUtils.FindSourceFilePath($"SectionRoot/Character/Enemy/{label}/{label}/{label}.mesh", config);
            return resolved;
        }
        return null;
    }

    public override async Task Setup(RszInstance rsz, RszImportType importType)
    {
        var node = GameObject.FindChild(NodeName);
        if (node != null) {
            node.GetParent().RemoveChild(node);
            node.QueueFree();
        }

        var path = GetMeshFilepath();
        if (string.IsNullOrEmpty(path)) return;

        var resource = Importer.FindOrImportResource<MeshResource>(path, ReachForGodot.GetAssetConfig(Game));
        if (resource == null) return;

        var meshResource = await resource.Import(false) as PackedScene;
        var inst = meshResource?.Instantiate<Node>();
        if (inst == null) return;

        inst.Name = NodeName;
        await GameObject.AddChildAsync(inst, GameObject.FindRszOwnerNode());
    }

    protected override void UpdateResourceName()
    {
        var id = ID;
        ResourceName = id <= 0 ? ClassBaseName : ClassBaseName + ": " + id;
    }
}
