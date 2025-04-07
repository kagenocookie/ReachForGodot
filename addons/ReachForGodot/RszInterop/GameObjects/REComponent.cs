namespace ReaGE;

using System;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using RszTool;

[GlobalClass, Tool]
public abstract partial class REComponent : REObject, ISerializationListener
{
    [ExportToolButton("Trigger pre-export action")]
    private Callable TriggerPreExport => Callable.From(PreExport);

    public GameObject GameObject { get; set; } = null!;
    public string Path => (GameObject?.Path) + ":" + Classname;

    public REComponent() { }
    public REComponent(SupportedGame game, string classname) : base(game, classname) {}

    public abstract Task Setup(RszImportType importType);

    public virtual void PreExport()
    {
    }

    public virtual void OnDestroy()
    {
    }

    public override string ToString() => (GameObject != null ? GameObject.ToString() + ":" : "") + (Classname ?? nameof(REComponent));

    public void OnBeforeSerialize()
    {
        GameObject = null!;
    }

    public void OnAfterDeserialize()
    {
    }

    public override Array<Dictionary> _GetPropertyList()
    {
        if (string.IsNullOrWhiteSpace(_classname) && GetType() != typeof(REComponentPlaceholder)) {
            _classname = TypeCache.GetClassnameForComponentType(GetType());
            if (_classname != null && Game != SupportedGame.Unknown) {
                ResetProperties();
            }
        }

        return base._GetPropertyList();
    }

    protected void EnsureResourceInContainer(REResource resource)
    {
        var container = GameObject?.FindRszOwner();
        if (container?.EnsureContainsResource(resource) == true) {
            GD.Print($"Resource {resource.ResourceName} registered in owner {container?.Path ?? Path}");
        }
    }
}
