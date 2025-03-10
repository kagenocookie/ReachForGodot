#if TOOLS
using System.Threading.Tasks;
using Godot;

namespace ReaGE.EditorLogic;

[Tool]
public partial class GameObjectCloneAction : NodeModificationAction
{
    private REGameObject source = null!;
    private REGameObject? clone;

    private REGameObject? activeNode;

    public REGameObject? Clone => clone;

    public override Node? ActiveNode => activeNode;

    private GameObjectCloneAction() {}

    public GameObjectCloneAction(REGameObject source)
    {
        this.source = source;
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
        clone.SetRecursiveOwner(source.Owner);
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