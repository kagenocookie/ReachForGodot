namespace ReaGE;

using System.Diagnostics;
using Godot;

[GlobalClass, Tool, Icon("res://addons/ReachForGodot/icons/prefab.png")]
public partial class PrefabNode : REGameObject, IRszContainer
{
    [Export] public AssetReference? Asset { get; set; }
    [Export] public REResource[]? Resources { get; set; }

    public bool IsEmpty => GetChildCount() == 0;
    public new string Path => $"{Asset?.AssetFilename}:{Name}";

    public void BuildTree(RszGodotConversionOptions options)
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
}