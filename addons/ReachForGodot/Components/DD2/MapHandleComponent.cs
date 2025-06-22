namespace ReaGE.Components.RE2;

using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, Tool, REComponentClass("via.navigation.AIMapSection", SupportedGame.DragonsDogma2)]
public partial class MapHandleComponent : REComponent, IAIMapComponent
{
	private static readonly REFieldAccessor HandleList = new REFieldAccessor("Maps")
        .Conditions(list => list.First(f => f.RszField.array));

    [REObjectFieldTarget("via.navigation.MapHandle")]
	private static readonly REFieldAccessor ResourceField = new REFieldAccessor("Resource")
        .Type(RszFieldType.Resource)
        .Conditions(list => list.First(f => f.RszField.type is RszFieldType.Resource or RszFieldType.String));

    bool _previewMainGroup = true;
    [Export] public bool PreviewMainGroup { get => _previewMainGroup; set { _previewMainGroup = value; ReloadPreview(); } }
    bool _previewSecondGroup = true;
    [Export] public bool PreviewSecondGroup { get => _previewSecondGroup; set { _previewSecondGroup = value; ReloadPreview(); } }
    private bool _showPreview;
    [Export] public bool ShowPreview {
        get => _showPreview;
        set { }
    }
    [ExportToolButton("Toggle preview")]
    private Callable TogglePreview => Callable.From(() => {
        _showPreview = !_showPreview;
        ReloadPreview();
    });

    private int previewIndex = 0;
    public REResource? MapResource => TryGetFieldValue(HandleList, out var res)
        ? res.AsGodotArray<REObject>().ElementAtOrDefault(previewIndex)?.GetField(ResourceField).As<REResource>()
        : null;

    public AimpFile? File => _showPreview ? _file : null;
    private AimpFile? _file;

    private void ReloadPreview()
    {
        if (GameObject == null) return;
        ReloadSourceFile();
        EmitChanged();
        GameObject.UpdateGizmos();
    }

    private bool ReloadSourceFile()
    {
        if (!_showPreview) return false;
        var resource = MapResource;
        if (resource?.Asset == null) return false;

        var rawResourceFilepath = resource.Asset.FindSourceFile(ReachForGodot.GetAssetConfig(Game));
        if (string.IsNullOrEmpty(rawResourceFilepath)) return false;

        var conv = AssetConverter.InstanceForGame(Game);
        _file = conv.Aimp.CreateFile(rawResourceFilepath);
        _file.Read();
        return true;
    }

	public override Task Setup(RszImportType importType)
	{
        return Task.CompletedTask;
	}
}