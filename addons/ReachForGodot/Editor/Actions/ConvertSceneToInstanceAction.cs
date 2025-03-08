#if TOOLS
using System.Threading.Tasks;
using Godot;

namespace ReaGE.EditorLogic;

[Tool]
public partial class ConvertSceneToInstanceAction : NodeModificationAction
{
    protected Node parentNode = null!;
    protected SceneFolder originalSceneNode = null!;
    protected int nodeIndex;
    protected SceneFolderEditableInstance? _clone;
    public SceneFolderEditableInstance? Clone => _clone;

    protected string originalSceneFilepath = null!;

    protected ConvertSceneToInstanceAction() {}

    public ConvertSceneToInstanceAction(SceneFolder originalSceneNode)
    {
        Debug.Assert(!string.IsNullOrEmpty(originalSceneNode.SceneFilePath));
        Debug.Assert(originalSceneNode.GetParent() != null);
        base.MergeMode = UndoRedo.MergeMode.Disable;
        this.originalSceneNode = originalSceneNode;
        originalSceneFilepath = originalSceneNode.SceneFilePath;
        parentNode = originalSceneNode.GetParent();
        nodeIndex = originalSceneNode.GetIndex();
    }

    public override void Do()
    {
        if (_clone == null || !IsInstanceValid(_clone)) {
            _clone = new SceneFolderEditableInstance() { Name = originalSceneNode.Name };
            _clone.CopyDataFrom(originalSceneNode);
            var scene = ResourceLoader.Load<PackedScene>(originalSceneFilepath).Instantiate<SceneFolder>();
            foreach (var child in scene.GetChildren()) {
                child.Owner = null;
                child.Reparent(_clone, false);
            }
        }

        parentNode.RemoveChild(originalSceneNode);
        parentNode.AddChild(_clone);
        parentNode.MoveChild(_clone, nodeIndex);
        if (parentNode is SceneFolderProxy proxy) {
            proxy.RefreshProxiedNode();
        }
        _clone.SetRecursiveOwner(parentNode.Owner ?? parentNode);
        EditorInterface.Singleton.EditNode(_clone);
    }

    public override void Undo()
    {
        if (_clone == null || !IsInstanceValid(_clone)) {
            if (parentNode is SceneFolderProxy proxy) {
                proxy.UnloadScene();
                proxy.LoadScene();
            }
            return;
        }

        parentNode.RemoveChild(_clone);
        if (originalSceneNode == null || !IsInstanceValid(originalSceneNode)) {
            originalSceneNode = ResourceLoader.Load<PackedScene>(originalSceneFilepath).Instantiate<SceneFolder>();
        }

        parentNode.AddChild(originalSceneNode);
        parentNode.MoveChild(originalSceneNode, nodeIndex);
        originalSceneNode.Owner = parentNode.Owner ?? parentNode;
        EditorInterface.Singleton.EditNode(originalSceneNode);
    }
}
#endif