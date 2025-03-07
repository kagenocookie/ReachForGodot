namespace RGE;

using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, Tool, Icon("res://addons/ReachForGodot/icons/gear.png")]
public partial class REGameObject : Node3D, ISerializationListener, ICloneable
{
    [Export] public SupportedGame Game { get; set; }
    [Export] public bool Enabled { get; set; } = true;
    [Export] public string Uuid { get; set; } = "00000000-0000-0000-0000-000000000000";
    [Export] public string? Prefab { get; set; }
    [Export] public string OriginalName { get; set; } = string.Empty;
    [Export] public REObject? Data { get; set; }
    private Godot.Collections.Array<REComponent> _components = null!;
    [Export] public Godot.Collections.Array<REComponent> Components
    {
        get => _components;
        set {
            _components = value;
            foreach (var comp in value) {
                comp.GameObject = this;
            }
        }
    }

    public Guid ObjectGuid => System.Guid.TryParse(Uuid, out var guid) ? guid : Guid.Empty;
    public SceneFolder? ParentFolder => this.FindNodeInParents<SceneFolder>();

    public IEnumerable<REGameObject> Children => this.FindChildrenByType<REGameObject>();
    public IEnumerable<REGameObject> AllChildren => this.FindChildrenByType<REGameObject>().SelectMany(c => new [] { c }.Concat(c.AllChildren));
    public IEnumerable<REGameObject> AllChildrenIncludingSelf => new [] { this }.Concat(AllChildren);

    public string Path => this is PrefabNode pfb
            ? pfb.Asset?.AssetFilename ?? SceneFilePath
            : Owner is SceneFolder scn
                ? $"{scn.Path}:/{scn.GetPathTo(this)}"
                : Owner is PrefabNode pfbParent
                    ? $"{pfbParent.Path}:/{pfbParent.GetPathTo(this)}"
                    : Owner != null ? Owner.GetPathTo(this) : Name;

    public override void _EnterTree()
    {
        Components ??= new();
        foreach (var comp in Components) {
            comp.GameObject = this;
        }
    }

    public void Clear()
    {
        this.ClearChildren();
        Components?.Clear();
    }

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

    public void AddComponent(REComponent component)
    {
        Components ??= new();
        Components.Add(component);
    }

    object ICloneable.Clone() => this.Clone();
    public REGameObject Clone()
    {
        var clone = RecursiveClone();
        return clone;
    }

    private REGameObject RecursiveClone()
    {
        // If it looks stupid that we're doing this manually instead of calling Duplicate(), that's because it is.
        // The issue is that Godot's Duplicate somehow modifies the original node's data to point to the clone.
        // Maybe it's just non-exported fields? Either way, doing it manually to be more reliable.
        var clone = CloneSelf();
        foreach (var child in GetChildren()) {
            if (child is ICloneable cloneable) {
                var childClone = (Node)cloneable.Clone();
                clone.AddChild(childClone);
            } else {
                clone.AddChild(child.Duplicate());
            }
        }
        return clone;
    }

    private REGameObject CloneSelf()
    {
        var clone = new REGameObject() {
            Name = Name,
            Game = Game,
            OriginalName = OriginalName,
            Uuid = Guid.NewGuid().ToString(),
            Data = Data?.Duplicate(true) as REObject,
            Components = new Godot.Collections.Array<REComponent>(),
            Enabled = Enabled,
            Prefab = Prefab,
        };
        clone.Transform = Transform;
        foreach (var comp in Components) {
            clone.Components.Add(comp.Clone(clone));
        }
        return clone;
    }

    public REComponent? GetComponent(string classname)
    {
        return Components?.FirstOrDefault(x => x is REComponent rec && rec.Classname == classname);
    }

    public override string ToString()
    {
        if (GetParent() is REGameObject parent) {
            var dedupId = parent.GetChildDeduplicationIndex(OriginalName, this);
            if (dedupId > 0) return $"{OriginalName} #{dedupId}";
        }

        return OriginalName;
    }

    public void OnBeforeSerialize()
    {
    }

    public void OnAfterDeserialize()
    {
        foreach (var comp in Components) {
            comp.GameObject = this;
        }
    }

    public Aabb CalculateBounds()
    {
        Aabb bounds = new Aabb();
        Vector3 origin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);

        Components ??= new();
        var visuals = Components.OfType<IVisualREComponent>();
        foreach (var vis in visuals) {
            var compBounds = vis.GetBounds();
            if (compBounds.Size.IsZeroApprox()) {
                origin = compBounds.Position;
            } else {
                bounds = bounds.Size.IsZeroApprox() ? compBounds : bounds.Merge(compBounds);
            }
        }

        foreach (var child in Children) {
            var childBounds = child.CalculateBounds();
            if (!childBounds.Size.IsZeroApprox()) {
                var transformedChildBounds = child.Transform * childBounds;
                bounds = bounds.Size.IsZeroApprox() ? transformedChildBounds : bounds.Merge(transformedChildBounds);
            }
        }

        if (bounds.Size.IsZeroApprox()) {
            if (!bounds.Position.IsZeroApprox()) {
                return bounds;
            }

            return new Aabb(origin.X == float.MaxValue ? Position : origin, Vector3.Zero);
        }

        return bounds;
    }
}
