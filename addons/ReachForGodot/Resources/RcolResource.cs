namespace ReaGE;

using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool]
public partial class RcolResource : REResourceProxy, IRszContainer
{
    REResource[]? IRszContainer.Resources { get => Array.Empty<REResource>(); set {} }

    public PackedScene? RcolScene => ImportedResource as PackedScene;
    public RcolRootNode? Instantiate() => RcolScene?.Instantiate<RcolRootNode>();

    string IRszContainer.Path => PathUtils.ImportPathToRelativePath(ResourcePath, ReachForGodot.GetAssetConfig(Game)) ?? ResourcePath;

    protected override Task<Resource?> Import()
    {
        var conv = new GodotRszImporter(ReachForGodot.GetAssetConfig(Game!)!, GodotRszImporter.importTreeChanges);
        conv.GenerateRcol(this);
        NotifyPropertyListChanged();
        return Task.FromResult(ImportedResource);
    }

    public void Clear()
    {
        __Data.Clear();
    }
}
