namespace RGE;

using System.Diagnostics;
using Godot;

[GlobalClass, Tool]
public partial class PrefabNode : REGameObject, IRszContainerNode
{
    [Export] public AssetReference? Asset { get; set; }
    [Export] public REResource[]? Resources { get; set; }

    public bool IsEmpty => GetChildCount() == 0;

    public void BuildTree(RszGodotConversionOptions options)
    {
        var sw = new Stopwatch();
        sw.Start();
        var conv = new RszGodotConverter(ReachForGodot.GetAssetConfig(Game!)!, options);
        conv.GeneratePrefabTree(this).ContinueWith((t) => {
            if (t.IsFaulted) {
                GD.Print("Tree rebuild failed:", t.Exception);
            } else {
                GD.Print("Tree rebuild finished in " + sw.Elapsed);
            }
            EditorInterface.Singleton.CallDeferred(EditorInterface.MethodName.MarkSceneAsUnsaved);
        });
    }
}