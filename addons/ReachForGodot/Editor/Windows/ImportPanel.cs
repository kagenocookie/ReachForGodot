using Godot;

namespace ReaGE;

[GlobalClass, Tool]
public partial class ImportPanel : Control
{
    [Signal] public delegate void ObjectDraggedEventHandler(GodotObject file);

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return true;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        var dict = data.AsGodotDictionary();
        if (dict.TryGetValue("type", out var typeVr)) {
            var type = typeVr.AsString();
            if (type == "nodes") {
                var nodes = dict["nodes"].AsGodotArray<NodePath>();
                var node = nodes.FirstOrDefault();
                if (node != null) {
                    EmitSignal(SignalName.ObjectDragged, GetTree().Root.GetNode(node));
                }
            } else if (type == "files") {
                var files = dict["files"].AsStringArray();
                var file = files.FirstOrDefault();
                if (file != null) {
                    EmitSignal(SignalName.ObjectDragged, ResourceLoader.Load(file));
                }
                GD.Print($"Dropped {type}: {atPosition} data: {data}");
            }
        }
    }
}
