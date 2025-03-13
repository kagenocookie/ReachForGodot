namespace ReaGE.Components;

using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, Tool, REComponentClass("app.ropeway.EnemyContextController", SupportedGame.ResidentEvil2RT)]
public partial class RE2EnemyContextController : REComponent
{
    public RE2EnemyContextController() { }
    public RE2EnemyContextController(SupportedGame game, string classname) : base(game, classname) {}

    private static readonly REObjectFieldAccessor InitialKind = new REObjectFieldAccessor("InitialKind").WithConditions("InitialKind");

    private const string NodeName = "__EnemyPreview";

    public enum KindID : int
	{
		em0000 = 0,
		em0100 = 1,
		em0200 = 2,
		em3000 = 3,
		em4000 = 4,
		em4100 = 5,
		em4400 = 6,
		em5000 = 7,
		em6000 = 8,
		em6100 = 9,
		em6200 = 10,
		em6300 = 11,
		em7000 = 12,
		em7100 = 13,
		em7110 = 14,
		em7200 = 15,
		em7300 = 16,
		em7400 = 17,
		em9000 = 18,
		em8000 = 19,
		em8100 = 20,
		em8200 = 21,
		em8300 = 22,
		em8400 = 23,
		em8500 = 24,
		em9999 = 25,
		MAX = 26,
		Invalid = -1,
	}

    public string? GetMeshFilepath()
    {
        if (TryGetFieldValue(InitialKind, out var val)) {
            var id = (KindID)val.AsInt32();
            var config = ReachForGodot.GetAssetConfig(Game);
            var resolved = PathUtils.FindSourceFilePath($"SectionRoot/Character/Enemy/{id}/Body/Body00/{id}_body00.mesh", config)
                        ?? PathUtils.FindSourceFilePath($"SectionRoot/Character/Enemy/{id}/{id}/{id}.mesh", config);
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
        await GameObject.AddChildAsync(inst, GameObject.Owner ?? GameObject);
    }

}
