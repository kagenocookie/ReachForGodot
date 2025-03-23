#if TOOLS
using Godot;

namespace ReaGE.EditorLogic;

[Tool]
public partial class GameObjectCloneAction : NodeModificationAction
{
    private GameObject source = null!;
    private Node owner = null!;
    private GameObject? clone;

    private GameObject? activeNode;

    public GameObject? Clone => clone;

    public override Node? ActiveNode => activeNode;

    private GameObjectCloneAction() {}

    public GameObjectCloneAction(GameObject source)
    {
        this.source = source;
        owner = EditorInterface.Singleton.GetEditedSceneRoot();
        base.MergeMode = UndoRedo.MergeMode.Disable;
        activeNode = source;
    }

    public override void Do()
    {
        if (clone == null || !IsInstanceValid(clone)) {
            clone = source.Clone();
        }

        source.GetParent().AddUniqueNamedChild(clone);
        source.GetParent().MoveChild(clone, source.GetIndex() + 1);
        clone.SetRecursiveOwner(owner);
        activeNode = clone;
    }

    public override void Undo()
    {
        if (clone != null) {
            source.GetParent().RemoveChild(clone);
        }
        activeNode = source;
    }
}
#endif