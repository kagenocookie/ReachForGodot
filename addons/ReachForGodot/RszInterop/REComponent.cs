namespace RGE;

using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, Tool]
public abstract partial class REComponent : Node
{
    [Export] public REObject? Data { get; set; }

    [ExportToolButton("Store modifications from nodes")]
    private Callable TriggerPreExport => Callable.From(PreExport);

    public string? Classname => Data?.Classname;
    public REGameObject? GameObject => this.FindNodeInParents<REGameObject>();

    public abstract Task Setup(IRszContainerNode root, REGameObject gameObject, RszInstance rsz, RszImportType importType);
    public virtual void PreExport()
    {
        if (string.IsNullOrEmpty(Data?.Classname)) {
            GD.PrintErr($"Component {Name} does not have any data declared! (path: {Owner.GetPathTo(this)})");
        }
    }

    public IEnumerable<IRszContainerNode> SerializedContainers => this.FindParentsByType<IRszContainerNode>();
    public T? FindResource<T>(string filepath) where T : REResource => SerializedContainers.Select(sc => sc.FindResource<T>(filepath)).FirstOrDefault();

    public override string ToString() => Classname ?? "REComponent";
}
