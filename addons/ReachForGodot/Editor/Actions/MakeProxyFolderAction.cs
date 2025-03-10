#if TOOLS
using System.Threading.Tasks;
using Godot;

namespace ReaGE.EditorLogic;

[Tool]
public partial class MakeProxyFolderAction : NodeModificationAction
{
    private SceneFolderProxy? proxyFolder;
    private SceneFolder? realFolder;
    private bool isProxyToNormalAction;
    private string assetFilename = null!;

    public override Node? ActiveNode => ActiveInstance;
    public SceneFolder? ActiveInstance { get; private set; }

    public override string Name => isProxyToNormalAction ? "Convert proxy to normal scene instance" : "Convert scene instance to proxy";

    private MakeProxyFolderAction() { }

    public MakeProxyFolderAction(SceneFolderProxy source)
    {
        isProxyToNormalAction = true;
        this.proxyFolder = source;
        base.MergeMode = UndoRedo.MergeMode.Disable;
        assetFilename = source.Asset?.AssetFilename!;
        ActiveInstance = source;
    }

    public MakeProxyFolderAction(SceneFolder realInstance)
    {
        isProxyToNormalAction = false;
        realFolder = realInstance;
        assetFilename = realInstance.Asset?.AssetFilename!;
        base.MergeMode = UndoRedo.MergeMode.Disable;
        ActiveInstance = realInstance;
    }

    public override void Do()
    {
        if (isProxyToNormalAction) {
            ConvertProxyToReal();
        } else {
            ConvertRealToProxy();
        }
    }

    public override void Undo()
    {
        if (!isProxyToNormalAction) {
            ConvertProxyToReal();
        } else {
            ConvertRealToProxy();
        }
    }

    private void ConvertProxyToReal()
    {
        if (proxyFolder == null || !IsInstanceValid(proxyFolder)) {
            GD.PrintErr("Can't convert proxy to concrete scene instance - proxy folder is invalid");
            return;
        }

        if (realFolder == null || !IsInstanceValid(realFolder)) {
            proxyFolder.LoadScene();
            if (proxyFolder.RealFolder == null) {
                GD.PrintErr("Failed to load proxied scene");
                return;
            }
            realFolder = proxyFolder.RealFolder;
        }
        proxyFolder.GetParent().EmplaceChild(proxyFolder, realFolder);
        proxyFolder.ShowLinkedFolder = false;
        ActiveInstance = realFolder;
    }

    private void ConvertRealToProxy()
    {
        if (realFolder == null || !IsInstanceValid(realFolder)) {
            GD.PrintErr("Can't convert concrete scene instance to proxy - folder is invalid");
            return;
        }

        var parent = realFolder.GetParent();
        var index = realFolder.GetIndex();
        if (proxyFolder == null || !IsInstanceValid(proxyFolder)) {
            proxyFolder = new SceneFolderProxy() {
                Game = realFolder.Game,
                OriginalName = realFolder.OriginalName,
                Asset = new AssetReference(realFolder.Asset!.AssetFilename),
                KnownBounds = realFolder.KnownBounds,
            };
        }

        parent.EmplaceChild(realFolder, proxyFolder);
        proxyFolder.RefreshProxiedNode();
        proxyFolder.ShowLinkedFolder = true;
        ActiveInstance = proxyFolder;
    }
}
#endif