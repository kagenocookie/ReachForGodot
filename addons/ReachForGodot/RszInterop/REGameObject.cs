namespace RGE;

using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, Tool]
public partial class REGameObject : Node3D
{
    [Export] public SupportedGame Game { get; set; }
    [Export] public bool Enabled { get; set; } = true;
    [Export] public string Uuid { get; set; } = "00000000-0000-0000-0000-000000000000";
    [Export] public string? Prefab { get; set; }
    [Export] public string OriginalName { get; set; } = string.Empty;
    [Export] public Node? ComponentContainer { get; set; }
    [Export] public REObject? Data { get; set; }

    public SceneFolder? SceneRoot => this.FindNodeInParents<SceneFolder>();

    public IEnumerable<REComponent> Components => ComponentContainer?.FindChildrenByType<REComponent>() ?? Array.Empty<REComponent>();

    public IEnumerable<REGameObject> Children => this.FindChildrenByType<REGameObject>();
    public IEnumerable<REGameObject> AllChildren => this.FindChildrenByType<REGameObject>().SelectMany(c => new [] { c }.Concat(c.AllChildren));
    public IEnumerable<REGameObject> AllChildrenIncludingSelf => new [] { this }.Concat(AllChildren);

    public int GetChildDeduplicationIndex(string name, REGameObject? relativeTo)
    {
        int i = 0;
        foreach (var child in Children) {
            if (child.OriginalName == name) {
                if (relativeTo == child) {
                    return i;
                }
                i++;
            }
        }
        return i;
    }

    public REGameObject? GetChild(string name, int deduplicationIndex)
    {
        var dupesFound = 0;
        foreach (var child in Children) {
            if (child.OriginalName == name) {
                if (dupesFound >= deduplicationIndex) {
                    return child;
                }

                dupesFound++;
            }
        }

        return null;
    }

    public void PreExport()
    {
        if (Data == null) {
            Data = new REObject(Game, "via.GameObject");
            Data.ResetProperties();
            Data.SetField(Data.TypeInfo.Fields[2], (byte)1); // one of these should probably be Enabled
            Data.SetField(Data.TypeInfo.Fields[3], (byte)1);
            Data.SetField(Data.TypeInfo.Fields[4], -1f);
        }

        Data.SetField(Data.TypeInfo.Fields[0], OriginalName);

        foreach (var comp in Components) {
            comp.PreExport();
        }

        foreach (var child in Children) {
            child.PreExport();
        }
    }

    public override void _EnterTree()
    {
        ComponentContainer?.SetDisplayFolded(true);
    }

    public Task AddComponent(REComponent component)
    {
        return EnsureComponentContainerSetup().AddChildAsync(component, Owner ?? this);
    }

    public REComponent? GetComponent(string classname)
    {
        return ComponentContainer?.FindChildWhere<REComponent>(x => x is REComponent rec && rec.Classname == classname);
    }

    private Node EnsureComponentContainerSetup()
    {
        if ((ComponentContainer ??= FindChild("Components")) == null) {
            AddChild(ComponentContainer = new Node() { Name = "Components" });
            ComponentContainer.Owner = Owner ?? this;
            MoveChild(ComponentContainer, 0);
        }

        return ComponentContainer;
    }
}
