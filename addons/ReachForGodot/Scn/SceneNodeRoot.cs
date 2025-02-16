namespace RFG;

using System;
using Godot;
using RszTool;

[GlobalClass, Tool]
public partial class SceneNodeRoot : Node
{
    [Export] public AssetReference? Asset { get; set; }

    [ExportToolButton("Regenerate tree")]
    private Callable BuildTreeButton => Callable.From(BuildTree);

    private ScnFile? scnFile;

    public static readonly Dictionary<string, SceneNodeRoot> activeScenes = new();

    public override void _EnterTree()
    {
        if (Asset?.AssetFilename != null) {
            activeScenes.Add(Asset.AssetFilename, this);
        }
    }

    public override void _ExitTree()
    {
        if (Asset?.AssetFilename != null) {
            activeScenes.Remove(Asset.AssetFilename);
        }
    }

    public void BuildTree()
    {
        GodotScnConverter.EnsureSafeJsonLoadContext();
        var conv = new GodotScnConverter(AssetConfig.Paths);
        conv.GenerateSceneTree(this);
    }
}