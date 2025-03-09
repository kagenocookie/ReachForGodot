namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, Tool]
public abstract partial class REComponent : REObject, ISerializationListener
{
    [ExportToolButton("Trigger pre-export action")]
    private Callable TriggerPreExport => Callable.From(PreExport);

    public REGameObject GameObject { get; set; } = null!;
    public string Path => (GameObject?.Path) + ":" + Classname;

    public REComponent() { }
    public REComponent(SupportedGame game, string classname) : base(game, classname) {}

    public abstract Task Setup(RszInstance rsz, RszImportType importType);
    public virtual void PreExport()
    {
    }

    public virtual void OnDestroy()
    {
    }

    public REComponent Clone(REGameObject gameObject)
    {
        var clone = (REComponent)Duplicate(true);
        clone.GameObject = gameObject;
        return clone;
    }

    public IRszContainerNode? GetContainer() => GameObject == null ? null
        : GameObject is IRszContainerNode rsz ? rsz
        : GameObject.Owner is IRszContainerNode owner ? owner
        : GameObject.FindNodeInParents<IRszContainerNode>();

    public override string ToString() => (GameObject != null ? GameObject.ToString() + ":" : "") + (Classname ?? nameof(REComponent));

    public void OnBeforeSerialize()
    {
        GameObject = null!;
    }

    public void OnAfterDeserialize()
    {
    }

    protected void EnsureResourceInContainer(REResource resource)
    {
        if (GetContainer()?.EnsureContainsResource(resource) == true) {
            GD.Print($"Resource {resource.ResourceName} registered in owner {GetContainer()?.Path ?? Path}");
        }
    }
}
