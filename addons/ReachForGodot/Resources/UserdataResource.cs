namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class UserdataResource : REResource, IRszContainer
{
    [Export] public REResource[]? Resources { get; set; }

    string IRszContainer.Path => PathUtils.ImportPathToRelativePath(ResourcePath, ReachForGodot.GetAssetConfig(Game)) ?? ResourcePath;

    public void Reimport()
    {
        var conv = new GodotRszImporter(ReachForGodot.GetAssetConfig(Game!)!, GodotRszImporter.importTreeChanges);
        conv.GenerateUserdata(this);
        NotifyPropertyListChanged();
    }

    public void Clear()
    {
        Resources = null;
        __Data.Clear();
    }
}
