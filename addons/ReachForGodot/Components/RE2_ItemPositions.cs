namespace ReaGE.Components.RE2;

using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, Tool, REComponentClass("app.ropeway.item.ItemPositions", SupportedGame.ResidentEvil2RT)]
public partial class RE2_ItemPositions : REComponent
{
	private static readonly REFieldAccessor ItemIdField = new REFieldAccessor("InitializeItemId");
	private static readonly REFieldAccessor WeaponIdField = new REFieldAccessor("InitializeWeaponId");
	private static readonly REFieldAccessor BulletIdField = new REFieldAccessor("InitializeBulletId");

	private const string NodeName = "__ItemPreview";

	public int ItemId => TryGetFieldValue(ItemIdField, out var val) && val.AsInt32() > 0 ? val.AsInt32()
		: TryGetFieldValue(BulletIdField, out var val2) ? val2.AsInt32()
		: 0;

	public int WeaponId => TryGetFieldValue(WeaponIdField, out var val) && val.AsInt32() > 0 ? val.AsInt32()
		: 0;

	private string? GetPrefabFilepath()
	{
		var id = ItemId;
		if (id > 0) {
			var label = TypeCache.GetEnumLabel(Game, "app.ropeway.gamemastering.Item.ID", id);
			return $"ObjectRoot/SetModel/sm7x_Item/common/item/tentative/{label}.pfb";
		}
		var wpid = WeaponId;
		if (wpid > 0) {
			var label = TypeCache.GetEnumLabel(Game, "app.ropeway.EquipmentDefine.WeaponType", wpid);
			return $"ObjectRoot/Prefab/UI/Weapon/{label}.pfb";
		}
		return null;
	}

	private async Task<MeshResource?> ExtractMeshFromPfb(string pfbPath)
	{
		var pfb = Importer.FindOrImportResource<PackedScene>(pfbPath, ReachForGodot.GetAssetConfig(Game));

		var root = pfb?.Instantiate<PrefabNode>();
		if (root == null) return null;

		if (root.IsEmpty) {
			try {
				var conv = new GodotRszImporter(ReachForGodot.GetAssetConfig(root.Game), GodotRszImporter.PresetImportModes.ImportMissingItems.ToOptions());
				await conv.RegeneratePrefabTree(root);
			} catch (Exception) {
				return null;
			}
		}

		var meshcomponent = root.GetComponentInChildren<REMeshComponent>();
		return meshcomponent?.Resource;
	}

	public override async Task Setup(RszInstance rsz, RszImportType importType)
	{
        var node = GameObject.FindChild(NodeName);
        if (node != null) {
            node.GetParent().RemoveChild(node);
            node.QueueFree();
        }

		var path = GetPrefabFilepath();
		if (string.IsNullOrEmpty(path)) return;

		var mesh = await ExtractMeshFromPfb(path);
		if (mesh == null) return;

		var meshResource = await mesh.Import(false) as PackedScene;
        var inst = meshResource?.Instantiate<Node>();
        if (inst == null) return;

        inst.Name = NodeName;
        await GameObject.AddChildAsync(inst, GameObject.Owner ?? GameObject);
	}
}