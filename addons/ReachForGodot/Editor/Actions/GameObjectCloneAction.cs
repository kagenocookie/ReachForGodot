#if TOOLS
using System.Threading.Tasks;
using Godot;

namespace ReaGE;

[Tool]
public partial class GameObjectCloneAction : NodeModificationAction
{
    private REGameObject source = null!;
    private REGameObject? clone;
    public REGameObject? Clone => clone;

    private GameObjectCloneAction() {}

    public GameObjectCloneAction(REGameObject source)
    {
        this.source = source;
        base.MergeMode = UndoRedo.MergeMode.Disable;
    }

    public override void Do()
    {
        if (clone == null || IsInstanceValid(clone)) {
            clone = source.Clone();
        }

        source.GetParent().AddUniqueNamedChild(clone);
        source.GetParent().MoveChild(clone, source.GetIndex() + 1);
        SetChildrenOwner(clone, source.Owner);
    }

    public override void Undo()
    {
        if (clone != null) {
            source.GetParent().RemoveChild(clone);
        }
    }

    private static void SetChildrenOwner(Node node, Node owner)
    {
        foreach (var child in node.GetChildren()) {
            child.Owner = owner;
            if (string.IsNullOrEmpty(child.SceneFilePath)) {
                SetChildrenOwner(child, owner);
            }
        }
    }
}
#endif