#if TOOLS
using System.Threading.Tasks;
using Godot;

namespace RGE;

[Tool]
public abstract partial class NodeModificationAction : GodotObject
{
    public virtual string Name => GetType().Name;
    public UndoRedo.MergeMode MergeMode { get; set; } = UndoRedo.MergeMode.Ends;

    public abstract void Do();
    public abstract void Undo();

    public void Trigger()
    {
        var undo = EditorInterface.Singleton.GetEditorUndoRedo();
        undo.CreateAction(Name, MergeMode);
        undo.AddDoMethod(this, MethodName.Do);
        undo.AddUndoMethod(this, MethodName.Undo);
        undo.CommitAction();
    }
}
#endif