using Godot;
using ReeLib;

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

    private Workspace? env;
    public Workspace Workspace => env ??= CreateWorkspace();

    private Workspace CreateWorkspace()
    {
        var config = GameConfig.CreateFromRepository(Game.ToShortName());
        config.GamePath = Paths.Gamedir ?? string.Empty;
        config.PakFiles = Paths.PakFiles;
        config.ChunkPath = Paths.ChunkPath;
        var ws = new Workspace(config);
        return ws;
    }

    public void ClearWorkspace()
    {
        env?.Dispose();
        env = null;
    }

    public bool IsValid => ReachForGodot.GetPaths(Game) != null;
    private GamePaths? _overridePaths;

    public GamePaths Paths {
        get => _overridePaths ?? ReachForGodot.GetPaths(Game) ?? throw new Exception("Paths not defined for game " + Game);
        set => _overridePaths = value;
    }

    public string ImportBasePath => ProjectSettings.GlobalizePath("res://" + AssetDirectory);

    [Export] public SupportedGame Game = SupportedGame.Unknown;
    [Export(PropertyHint.Dir)] public string AssetDirectory = "assets/";

#if TOOLS
    [ExportToolButton("Import assets...")]
    private Callable ImportBtn => Callable.From(() => ReachForGodotPlugin.Instance.OpenAssetImporterWindow(this));

    [ExportToolButton("Packed file browser")]
    private Callable BrowserBtn => Callable.From(() => ReachForGodotPlugin.Instance.OpenPackedAssetBrowser(this));

    [ExportToolButton("DEV: Build all RSZ data")]
    private Callable InferRszData => Callable.From(() => ReachForGodotPlugin.Instance.CheckInferrableRszData(this));
#endif

    private void InvokeCallback(Callable callable)
    {
        callable.Call();
    }
}