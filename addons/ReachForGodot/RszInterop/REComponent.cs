namespace RGE;

using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, Tool]
public abstract partial class REComponent : REObject, ISerializationListener
{
    [ExportToolButton("Store modifications from nodes")]
    private Callable TriggerPreExport => Callable.From(PreExport);

    public REGameObject GameObject { get; set; } = null!;

    public REComponent() { }
    public REComponent(SupportedGame game, string classname) : base(game, classname) {}

    public abstract Task Setup(REGameObject gameObject, RszInstance rsz, RszImportType importType);
    public virtual void PreExport()
    {
    }

    public virtual void OnDestroy()
    {
    }

    public IEnumerable<IRszContainerNode> SerializedContainers => GameObject is IRszContainerNode rsz
        ? new [] { rsz }.Concat(GameObject.FindParentsByType<IRszContainerNode>())
        : GameObject.FindParentsByType<IRszContainerNode>();
    public T? FindResource<T>(string filepath) where T : REResource => SerializedContainers.Select(sc => sc.FindResource<T>(filepath)).FirstOrDefault();

    public override string ToString() => (GameObject != null ? GameObject.ToString() + ":" : "") + (Classname ?? nameof(REComponent));

    public void OnBeforeSerialize()
    {
        GameObject = null!;
    }

    public void OnAfterDeserialize()
    {
    }
}
