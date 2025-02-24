namespace RGE;

using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, Tool]
public partial class REGameObject : Node3D
{
    [Export] public int ObjectId { get; set; } = -1;
    [Export] public bool Enabled { get; set; } = true;
    [Export] public string Uuid { get; set; } = "00000000-0000-0000-0000-000000000000";
    [Export] public string? Prefab { get; set; }
    [Export] public string OriginalName { get; set; } = string.Empty;
    [Export] public string? Tags { get; set; }
    [Export] public Node? ComponentContainer { get; set; }

    public SceneFolder? SceneRoot => this.FindNodeInParents<SceneFolder>();

    public IEnumerable<REComponent> Components => ComponentContainer?.FindChildrenByType<REComponent>() ?? Array.Empty<REComponent>();
    public IEnumerable<REGameObject> Children => this.FindChildrenByType<REGameObject>();
    public IEnumerable<REGameObject> AllChildren => this.FindChildrenByType<REGameObject>().SelectMany(c => new [] { c }.Concat(c.AllChildren));
    public IEnumerable<REGameObject> AllChildrenIncludingSelf => new [] { this }.Concat(AllChildren);

    public RszInstance GetData(RszClass gameobjectClass)
    {
        return new RszInstance(gameobjectClass, -1, null, new object[] {
            OriginalName,
            Tags ?? string.Empty,
            (byte)0, // v2
            (byte)0, // v3
            -1f // v4
        });
    }

    public override void _EnterTree()
    {
        ComponentContainer?.SetDisplayFolded(true);
    }

    public Task AddComponent(REComponent component)
    {
        return EnsureComponentContainerSetup().AddChildAsync(component, Owner);
    }

    public REComponent? GetComponent(string classname)
    {
        return ComponentContainer?.FindChildWhere<REComponent>(x => x is REComponent rec && rec.Classname == classname);
    }

    private Node EnsureComponentContainerSetup()
    {
        if ((ComponentContainer ??= FindChild("Components")) == null) {
            AddChild(ComponentContainer = new Node() { Name = "Components" });
            ComponentContainer.Owner = Owner;
            MoveChild(ComponentContainer, 0);
        }

        return ComponentContainer;
    }
}
