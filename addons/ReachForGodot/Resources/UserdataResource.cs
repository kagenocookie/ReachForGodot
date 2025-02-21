namespace RGE;

using System;
using Godot;
using RszTool;

[GlobalClass, Tool]
public partial class UserdataResource : REResource, IRszContainerNode
{
    [Export] public REResource[]? Resources { get; set; }
    public int ObjectId { get; set; }

    public bool IsEmpty => false;

    [ExportToolButton("Re-import")]
    private Callable ReimportFileButton => Callable.From(Reimport);

    public void Reimport()
    {
        var conv = new RszGodotConverter(ReachForGodot.GetAssetConfig(Game!)!, RszGodotConverter.importMissing);
        conv.GenerateUserdata(this);
        NotifyPropertyListChanged();
    }

    public void Clear()
    {
        Resources = null;
        __Data.Clear();
    }
}
