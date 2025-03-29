namespace ReaGE;

using System.Diagnostics;
using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool, Icon("res://addons/ReachForGodot/icons/prefab.png"), ResourceHolder("pfb", RESupportedFileFormats.Prefab)]
public partial class PrefabNode : GameObject, IRszContainer, IImportableAsset
{
    [Export] public AssetReference? Asset { get; set; }
    [Export] public REResource[]? Resources { get; set; }

    public bool IsEmpty => GetChildCount() == 0;
    public new string Path => $"{Asset?.AssetFilename}:{Name}";

    public void BuildTree(GodotImportOptions options)
    {
        var sw = new Stopwatch();
        sw.Start();
        var conv = new GodotRszImporter(ReachForGodot.GetAssetConfig(Game!)!, options);
        conv.RegeneratePrefabTree(this).ContinueWith((t) => {
            if (t.IsCompletedSuccessfully) {
                GD.Print("Tree rebuild finished in " + sw.Elapsed);
            } else {
                GD.Print("Tree rebuild failed:", t.Exception);
            }
            EditorInterface.Singleton.CallDeferred(EditorInterface.MethodName.MarkSceneAsUnsaved);
        });
    }

    IEnumerable<(string label, PresetImportModes importMode)> IImportableAsset.SupportedImportTypes => [
        ("Import anything missing", PresetImportModes.ImportTreeChanges),
        ("Discard and reimport structure", PresetImportModes.ReimportStructure),
        ("Fully reimport all resources", PresetImportModes.FullReimport),
    ];

    async Task<bool> IImportableAsset.Import(string resolvedFilepath, GodotRszImporter importer)
    {
        await importer.RegeneratePrefabTree(this);
        return true;
    }
}