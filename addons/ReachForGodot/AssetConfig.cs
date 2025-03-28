#if TOOLS
#nullable enable

using CustomFileBrowser;
using Godot;

namespace ReaGE;

[GlobalClass, Tool]
public partial class AssetConfig : Resource
{
    private const string ConfigResource = "res://asset_config.tres";

    private static AssetConfig? _instance;
    public static AssetConfig DefaultInstance {
        get {
            if (_instance == null) {
                if (!ResourceLoader.Exists(ConfigResource)) {
                    _instance = new AssetConfig() { ResourcePath = ConfigResource };
                    ResourceSaver.Save(_instance);
                } else {
                    _instance = ResourceLoader.Load<AssetConfig>("res://asset_config.tres");
                }
            }
            return _instance;
        }
    }

    public bool IsValid => ReachForGodot.GetPaths(Game) != null;

    public GamePaths Paths => ReachForGodot.GetPaths(Game) ?? throw new Exception("Paths not defined for game " + Game);

    public string ImportBasePath => ProjectSettings.GlobalizePath("res://" + AssetDirectory);

    [Export] public SupportedGame Game = SupportedGame.Unknown;
    [Export(PropertyHint.Dir)] public string AssetDirectory = "assets/";

    [ExportToolButton("Import assets...")]
    private Callable ImportBtn => Callable.From(() => ReachForGodotPlugin.Instance.OpenAssetImporterWindow(this));

    [ExportToolButton("Packed file browser")]
    private Callable BrowserBtn => Callable.From(() => ReachForGodotPlugin.Instance.OpenPackedAssetBrowser(this));

    [ExportToolButton("DEV: Build all RSZ data")]
    private Callable InferRszData => Callable.From(() => ReachForGodotPlugin.Instance.FetchInferrableRszData(this));

    private void InvokeCallback(Callable callable)
    {
        callable.Call();
    }
}
#endif