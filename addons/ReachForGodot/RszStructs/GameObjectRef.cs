namespace RGE;

using Godot;
using Godot.Collections;

[GlobalClass, Tool]
public partial class GameObjectRef : Resource
{
    [Export] private string? guid;

    private NodePath? _path;

    [Export]
    public NodePath? Path {
        get => _path;
        set {
#if TOOLS
            if (value != null && value != _path) {
                var sourceNode = EditorInterface.Singleton.GetSelection().GetSelectedNodes().FirstOrDefault();
                guid = sourceNode?.GetNodeOrNull<REGameObject>(value)?.Uuid ?? Guid.Empty.ToString();
            }
#endif
            _path = value;
        }
    }

    public Guid TargetGuid {
        get => Guid.TryParse(guid, out var parsed) ? parsed : Guid.Empty;
        set => guid = value.ToString();
    }

    public GameObjectRef()
    {
    }

    public GameObjectRef(Guid target, NodePath? path = null)
    {
        this.Path = path;
        this.guid = target.ToString();
    }

    public GameObjectRef(string target, NodePath? path = null)
    {
        this.Path = path;
        this.guid = target;
    }

    public override void _ValidateProperty(Dictionary property)
    {
        if (property["name"].AsStringName() == PropertyName.Path) {
            property["hint"] = (int)PropertyHint.NodePathValidTypes;
            property["hint_string"] = nameof(REGameObject);
        }
        base._ValidateProperty(property);
    }

    public REGameObject? Resolve(REGameObject sourceGameObject)
    {
        if (Path?.IsEmpty != false) return null;

        return sourceGameObject.GetNodeOrNull<REGameObject>(Path);
    }

    public Guid ResolveGuid(REGameObject sourceGameObject)
    {
        if (Path?.IsEmpty != false) return TargetGuid;

        return sourceGameObject.GetNodeOrNull<REGameObject>(Path)?.ObjectGuid ?? TargetGuid;
    }

    public bool IsEmpty => Path?.IsEmpty != false;

    public override string ToString()
    {
        return $"{Path?.ToString()} ({guid})";
    }
}
