#if TOOLS
using System.Threading.Tasks;
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
        Importer.QueueFileRescan();
    }
}
#endif
