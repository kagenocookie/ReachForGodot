#if TOOLS
#nullable enable

using Godot;

namespace RGE;

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

    public GamePaths Paths => ReachForGodot.GetPaths(Game) ?? throw new Exception("Paths not defined for game " + Game);

    [Export] public SupportedGame Game = SupportedGame.Unknown;
    [Export(PropertyHint.Dir)] public string AssetDirectory = "assets/";

    [ExportToolButton("Import assets...")]
    private Callable ImportBtn => Callable.From(() => ReachForGodotPlugin.Instance.OpenAssetImporterWindow(this));

    private void InvokeCallback(Callable callable)
    {
        callable.Call();
    }
}
#endif