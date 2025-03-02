namespace RGE;

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool]
public partial class SceneFolderProxy : SceneFolder
{
    [Export] public bool Enabled
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
        RealFolder ??= GetChildOrNull<SceneFolder>(0);
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
                RealFolder.QueueFree();
                RealFolder = null;
            }
            _contentScene = null;
            return;
        }

        if (RealFolder != null) return;
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

    public override void BuildTree(RszGodotConversionOptions options)
    {
        var sw = new Stopwatch();
        sw.Start();
        var config = ReachForGodot.GetAssetConfig(Game)!;
        var conv = new RszGodotConverter(config, options);

        if (_contentScene == null) {
            _contentScene = Importer.FindOrImportResource<PackedScene>(Asset!.AssetFilename, conv.AssetConfig)!;
            EditorInterface.Singleton.CallDeferred(EditorInterface.MethodName.MarkSceneAsUnsaved);
        }

        var tempInstance = _contentScene!.Instantiate<SceneFolder>();
        conv.RegenerateSceneTree(tempInstance).ContinueWith((t) => {
            if (t.IsCompletedSuccessfully) {
                GD.Print("Tree rebuild finished in " + sw.Elapsed);
            } else {
                GD.Print($"Tree rebuild failed after {sw.Elapsed}:", t.Exception);
                if (Enabled && _contentScene != null) {
                    LoadScene();
                }
            }
        });
    }

    public override void RecalculateBounds(bool deepRecalculate)
    {
        var wasShown = Enabled;
        Enabled = true;

        if (RealFolder != null) {
            RealFolder.RecalculateBounds(deepRecalculate);
            KnownBounds = RealFolder.KnownBounds;
        }

        if (wasShown != Enabled) {
            if (!wasShown && KnownBounds.Size.IsZeroApprox() && KnownBounds.Position.IsZeroApprox()) {
                GD.PrintErr("Try pressing the recalculate button again after the actual scene is loaded in. Sorry for the inconvenience.");
            } else {
                Enabled = wasShown;
            }
        }
    }

    public void LoadAllChildren(bool load)
    {
        Enabled = load;
        foreach (var ch in AllSubfolders.OfType<SceneFolderProxy>()) {
            ch.Enabled = load;
            ch.LoadAllChildren(load);
        }
    }
}
