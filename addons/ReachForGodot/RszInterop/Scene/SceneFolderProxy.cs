namespace ReaGE;

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;
using ReaGE.EditorLogic;

[GlobalClass, Tool, Icon("res://addons/ReachForGodot/icons/folder_link.png")]
public partial class SceneFolderProxy : SceneFolder
{
    [Export] public bool ShowLinkedFolder
    {
        get => _enabled;
        set => ChangeEnabled(value);
    }
    private bool _enabled = false;

    private PackedScene? _contentScene { get; set; }
    public SceneFolder? RealFolder { get; private set; }
    public PackedScene? Contents => _contentScene ??=
        (Asset == null ? null : Importer.FindOrImportResource<PackedScene>(Asset.AssetFilename, ReachForGodot.GetAssetConfig(Game)));

    private void ChangeEnabled(bool value)
    {
        if (value == _enabled) return;
        SetShowScene(_enabled = value);
    }

    public override void _EnterTree()
    {
        if (RealFolder == null) RefreshProxiedNode();
        SetShowScene(_enabled);
    }

    public void LoadScene()
    {
        SetShowScene(true);
    }

    public void UnloadScene()
    {
        SetShowScene(false);
    }

    private void SetShowScene(bool show)
    {
        if (!show) {
            if (RealFolder != null) {
                RemoveChild(RealFolder);
                if (IsInsideTree()) {
                    RealFolder.QueueFree();
                }
                RealFolder = null;
            }
            _contentScene = null;
            return;
        }

        if ((RealFolder ??= GetChildOrNull<SceneFolder>(0)) != null) return;

        if (_contentScene == null) {
            if (Asset == null) return;
            _contentScene = Importer.FindOrImportResource<PackedScene>(Asset.AssetFilename, ReachForGodot.GetAssetConfig(Game));
            if (_contentScene == null) {
                GD.PrintErr("Not found proxy source scene " + Asset.AssetFilename);
                return;
            }
        }

        RealFolder = _contentScene?.Instantiate<SceneFolder>(PackedScene.GenEditState.Instance);
        if (RealFolder != null) {
            RealFolder.Name = Name;
            AddChild(RealFolder);
            RealFolder.Owner = Owner;
        }
    }

    public override void RecalculateBounds(bool deepRecalculate)
    {
        var wasShown = ShowLinkedFolder;
        ShowLinkedFolder = true;

        if (RealFolder != null) {
            RealFolder.RecalculateBounds(deepRecalculate);
            KnownBounds = RealFolder.KnownBounds;
        }

        if (wasShown != ShowLinkedFolder) {
            if (!wasShown && KnownBounds.Size.IsZeroApprox() && KnownBounds.Position.IsZeroApprox()) {
                GD.PrintErr("Try pressing the recalculate button again after the actual scene is loaded in. Sorry for the inconvenience.");
            } else {
                ShowLinkedFolder = wasShown;
            }
        }
    }

    public SceneFolder ReplaceWithRealInstance()
    {
        var action = new MakeProxyFolderAction(this);
        action.Do();
        return action.ActiveInstance!;
    }

    public void LoadAllChildren(bool load)
    {
        ShowLinkedFolder = load;
        foreach (var ch in AllSubfolders.OfType<SceneFolderProxy>()) {
            ch.ShowLinkedFolder = load;
            ch.LoadAllChildren(load);
        }
    }

    public void RefreshProxiedNode()
    {
        RealFolder = GetChildOrNull<SceneFolder>(0);
    }
}
