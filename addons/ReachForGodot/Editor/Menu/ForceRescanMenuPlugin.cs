#if TOOLS
using Godot;

namespace ReaGE;

public partial class ForceRescanMenuPlugin : EditorContextMenuPlugin
{
    private Texture2D? _logo;
    private Texture2D Logo => _logo ??= ResourceLoader.Load<Texture2D>("res://addons/ReachForGodot/icons/logo.png");

    public override void _PopupMenu(string[] paths)
    {
        AddContextMenuItem("Force rescan resources", Callable.From((Godot.Collections.Array _) => DoRescan()));
    }

    private void DoRescan()
    {
        var fs = EditorInterface.Singleton.GetResourceFilesystem();
        if (!fs.IsScanning()) fs.CallDeferred(EditorFileSystem.MethodName.Scan);
    }
}
#endif
