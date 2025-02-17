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

    [ExportToolButton("Open source file")]
    private Callable OpenSourceFile => Callable.From(() => ((IRszContainerNode)this).OpenSourceFile());

    [ExportToolButton("Find me something to look at")]
    public Callable Find3DNodeButton => Callable.From(() => ((IRszContainerNode)this).Find3DNode());

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
