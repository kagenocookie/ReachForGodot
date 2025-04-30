namespace ReaGE;
using Godot;

[GlobalClass, Tool, Icon("res://addons/ReachForGodot/icons/prefab.png")]
public partial class PrefabNode : GameObject, IRszContainer, IImportableAsset
{
    [Export] public AssetReference? Asset { get; set; }
    [Export] public REResource[]? Resources { get; set; }

    public bool IsEmpty => GetChildCount() == 0 && (Components == null || Components.Count == 0);
    public new string Path => $"{Asset?.AssetFilename}:{Name}";

    IEnumerable<(string label, GodotImportOptions importMode)> IImportableAsset.SupportedImportTypes => [
        ("Import anything missing", GodotImportOptions.importTreeChanges),
        ("Discard and reimport structure", GodotImportOptions.forceReimportStructure),
        ("Fully reimport all resources", GodotImportOptions.fullReimport),
    ];
}