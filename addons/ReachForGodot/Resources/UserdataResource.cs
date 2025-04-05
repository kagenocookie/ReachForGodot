namespace ReaGE;

using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool, ResourceHolder("user", SupportedFileFormats.Userdata)]
public partial class UserdataResource : REResource, IRszContainer, IImportableAsset
{
    public UserdataResource() : base(SupportedFileFormats.Userdata)
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
        CreateImporter().User.ImportFromFile(this);
        NotifyPropertyListChanged();
    }

    public void Clear()
    {
        Resources = null;
        if (!string.IsNullOrEmpty(Data?.Classname)) {
            Data.ResetProperties();
        }
    }
}
