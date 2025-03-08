#if TOOLS
using System.Threading.Tasks;
using Godot;

namespace ReaGE.EditorLogic;

[Tool]
public partial class RestoreSceneInstanceToLinkAction : ConvertSceneToInstanceAction
{
    private RestoreSceneInstanceToLinkAction() {}

    public RestoreSceneInstanceToLinkAction(SceneFolderEditableInstance instanceScene)
    {
        base.MergeMode = UndoRedo.MergeMode.Disable;
        _clone = instanceScene;
        originalSceneFilepath = instanceScene.Asset!.GetImportFilepath(ReachForGodot.GetAssetConfig(instanceScene.Game))!;
        parentNode = instanceScene.GetParent();
        nodeIndex = instanceScene.GetIndex();
    }

    public override void Do()
    {
        if (originalSceneNode == null || !IsInstanceValid(originalSceneNode)) {
            originalSceneNode = ResourceLoader.Load<PackedScene>(originalSceneFilepath).Instantiate<SceneFolder>();
        }

        base.Undo();
    }

    public override void Undo()
    {
        base.Do();
    }
}
#endif