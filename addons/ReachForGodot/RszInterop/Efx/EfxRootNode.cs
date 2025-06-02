namespace ReaGE.EFX;

using Godot;
using RszTool.Efx;

[GlobalClass, Tool, Icon("res://addons/ReachForGodot/icons/efx_small.png")]
public partial class EfxRootNode : Node3D, IImportableAsset, IExportableAsset, IExpressionParameterSource
{
    [Export] public SupportedGame Game { get; set; }
    [Export] public EfxVersion Version { get; set; }
    [Export] public AssetReference? Asset { get; set; }
    [Export] public Godot.Collections.Array<EfxFieldParameter> FieldParameterValues = new();
    [Export] public Godot.Collections.Array<EfxExpressionParameter> ExpressionParameters = new();
    // public EfxResource? Resource { get; set; }
    private EfxResource? _resource;
    public EfxResource? Resource {
        get => _resource ??= Importer.FindOrImportResource<EfxResource>(Asset, ReachForGodot.GetAssetConfig(Game), !string.IsNullOrEmpty(SceneFilePath));
        set => _resource = value;
    }

    public bool IsEmpty => GetChildCount() == 0;
    public string Path => Asset?.AssetFilename ?? Resource?.ResourcePath ?? (!string.IsNullOrEmpty(SceneFilePath) ? SceneFilePath : Name);

    [Export] public int UnknownNum;
    [Export] public int Flags;

    public IEnumerable<EfxNode> Entries => this.FindChildrenByType<EfxNode>();
    public IEnumerable<EfxActionNode> Actions => this.FindChildrenByType<EfxActionNode>().Where(e => e is not EfxNode);

    public EFXExpressionParameter? FindParameterByHash(uint hash)
    {
        foreach (var p in ExpressionParameters) {
            if (p.NameHash == hash) return p.GetExported();
        }
        return null;
    }
}