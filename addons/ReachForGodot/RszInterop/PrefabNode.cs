namespace RGE;

using System;
using System.Diagnostics;
using Godot;
using RszTool;

[GlobalClass, Tool]
public partial class PrefabNode : REGameObject, IRszContainerNode
{
    [Export] public SupportedGame Game { get; set; }
    [Export] public AssetReference? Asset { get; set; }
    [Export] public REResource[]? Resources { get; set; }

    public bool IsEmpty => GetChildCount() == 0;

    public void BuildTree(RszGodotConversionOptions options)
    {
        var conv = new RszGodotConverter(ReachForGodot.GetAssetConfig(Game!)!, options);
        conv.GeneratePrefabTree(this).ContinueWith((t) => {
            if (t.IsFaulted) {
                GD.Print("Tree rebuild failed:", t.Exception);
            } else {
                GD.Print("Tree rebuild finished");
            }
            EditorInterface.Singleton.CallDeferred(EditorInterface.MethodName.MarkSceneAsUnsaved);
        });
    }
}