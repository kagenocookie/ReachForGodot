namespace ReaGE;

using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool, ResourceHolder("user", RESupportedFileFormats.Userdata)]
public partial class UserdataResource : REResource, IRszContainer, IImportableAsset
{
    public UserdataResource() : base(RESupportedFileFormats.Userdata)
    {
    }

    private REObject? _data;
    [Export] public REObject Data
    {
        get => _data ??= new REObject(Game, null);
        set => _data = value;
    }
    [Export] public REResource[]? Resources { get; set; }

    public string? Classname => _data?.Classname;

    string IRszContainer.Path => PathUtils.ImportPathToRelativePath(ResourcePath, ReachForGodot.GetAssetConfig(Game)) ?? ResourcePath;

    public bool IsEmpty => Data?.IsEmpty != false || string.IsNullOrEmpty(Data?.Classname);

    public void Reimport()
    {
        _ = Import(string.Empty, new GodotRszImporter(ReachForGodot.GetAssetConfig(Game!)!, GodotRszImporter.importTreeChanges));
        NotifyPropertyListChanged();
    }

    public void Clear()
    {
        Resources = null;
        if (!string.IsNullOrEmpty(Data?.Classname)) {
            Data.ResetProperties();
        }
    }

    public Task<bool> Import(string resolvedFilepath, GodotRszImporter importer)
    {
        importer.GenerateUserdata(this);
        return Task.FromResult(true);
    }
}
