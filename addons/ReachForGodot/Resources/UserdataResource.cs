namespace RFG;

using System;
using Godot;
using RszTool;

[GlobalClass, Tool]
public partial class UserdataResource : REResource, IRszContainerNode
{
    [Export] public REResource[]? Resources { get; set; }
    public int ObjectId { get; set; }

    public bool IsEmpty => false;

    [ExportToolButton("Rebuild")]
    private Callable RebuildFileButton => Callable.From(Rebuild);

    public void Rebuild()
    {
        using var conv = new RszGodotConverter(ReachForGodot.GetAssetConfig(Game!)!, false);
        conv.GenerateUserdata(this);
    }

    public void Clear()
    {
        Resources = null;
        Data.Clear();
    }
}
