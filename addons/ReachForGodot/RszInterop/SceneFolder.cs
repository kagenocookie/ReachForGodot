namespace RGE;

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool, Icon("res://addons/ReachForGodot/icons/folder.png")]
public partial class SceneFolder : Node, IRszContainerNode
{
    [Export] public SupportedGame Game { get; set; }
    [Export] public AssetReference? Asset { get; set; }
    [Export] public REResource[]? Resources { get; set; }
    [Export] public string? OriginalName { get; set; }
    [Export] public Aabb KnownBounds { get; set; }

    public bool IsEmpty => GetChildCount() == 0;
    public SceneFolder? ParentFolder => GetParent()?.FindNodeInParents<SceneFolder>();

    public IEnumerable<SceneFolder> Subfolders => this.FindChildrenByType<SceneFolder>() ?? Array.Empty<SceneFolder>();
    public IEnumerable<SceneFolder> AllSubfolders => Subfolders.SelectMany(f => new [] { f }.Concat(f.AllSubfolders));
    public IEnumerable<REGameObject> ChildObjects => this.FindChildrenByType<REGameObject>();

    public string Path => Owner is SceneFolder ownerScn ? $"{ownerScn.Asset?.AssetFilename}:{Owner.GetPathTo(this)}" : $"{Asset?.AssetFilename}:{Name}";

    public void AddFolder(SceneFolder folder)
    {
        Debug.Assert(folder != this);
        AddChild(folder);
        folder.Owner = Owner ?? this;
    }

    public void RemoveFolder(SceneFolder folder)
    {
        if (folder.GetParent() == this) {
            RemoveChild(folder);
            folder.QueueFree();
        }
    }

    private const float CameraAutoRepositionMaxDistanceSquared = 50 * 50;
    public override void _Ready()
    {
        if (Owner == null && GetParent() is SubViewport) {
            CallDeferred(MethodName.EditorTryRepositionCamera);
        }
    }

    private void EditorTryRepositionCamera()
    {
        var cam = EditorInterface.Singleton.GetEditorViewport3D().GetCamera3D();
        if (cam != null) {
            var campos = cam.GlobalPosition;
            var bounds = KnownBounds;
            if (!bounds.Position.IsZeroApprox() && campos.DistanceSquaredTo(bounds.GetCenter()) > CameraAutoRepositionMaxDistanceSquared) {
                EditorRepositionCamera();
            }
        }
    }

    public void EditorRepositionCamera()
    {
        var cam = EditorInterface.Singleton.GetEditorViewport3D().GetCamera3D();
        if (cam != null) {
            var campos = cam.GlobalPosition;
            var bounds = KnownBounds;
            var size = !bounds.Size.IsZeroApprox() ? bounds.Size.LimitLength(50) : new Vector3(30, 30, 30);
            cam.LookAtFromPosition(bounds.GetCenter() + size, bounds.GetCenter());
        }
    }

    public void PreExport()
    {
        foreach (var obj in ChildObjects) {
            obj.PreExport();
        }

        foreach (var sub in Subfolders) {
            sub.PreExport();
        }
    }

    public void Clear()
    {
        this.ClearChildren();
        Resources = Array.Empty<REResource>();
    }

    public SceneFolder? GetFolder(string name)
    {
        return this.FindChildWhere<SceneFolder>(c => string.IsNullOrEmpty(c.OriginalName) ? c.Name == name : c.OriginalName == name);
    }

    public REGameObject? GetGameObject(string name, int deduplicationIndex)
    {
        var dupesFound = 0;
        foreach (var child in this.FindChildrenByType<REGameObject>()) {
            if (child.OriginalName == name) {
                if (dupesFound >= deduplicationIndex) {
                    return child;
                }

                dupesFound++;
            }
        }

        return null;
    }

    public virtual void BuildTree(RszGodotConversionOptions options)
    {
        var sw = new Stopwatch();
        sw.Start();
        var conv = new GodotRszImporter(ReachForGodot.GetAssetConfig(Game!)!, options);
        conv.RegenerateSceneTree(this).ContinueWith((t) => {
            if (t.IsCompletedSuccessfully) {
                GD.Print("Tree rebuild finished in " + sw.Elapsed);
            } else {
                GD.Print("Tree rebuild failed:", t.Exception);
            }
            EditorInterface.Singleton.CallDeferred(EditorInterface.MethodName.MarkSceneAsUnsaved);
        });
    }

    public virtual void RecalculateBounds(bool deepRecalculate)
    {
        Aabb bounds = new Aabb();

        foreach (var go in this.FindChildrenByType<REGameObject>()) {
            var childBounds = go.CalculateBounds();
            if (!childBounds.Size.IsZeroApprox()) {
                bounds = bounds.Position.IsZeroApprox() && bounds.Size.IsZeroApprox() ? childBounds : bounds.Merge(childBounds);
            } else if (!childBounds.Position.IsZeroApprox()) {
                bounds = bounds.Position.IsZeroApprox() ? childBounds : bounds.Expand(childBounds.Position);
            }
        }

        foreach (var subfolder in Subfolders) {
            var subBounds = subfolder.KnownBounds;
            if (!subBounds.Size.IsZeroApprox()) {
                bounds = bounds.Size.IsZeroApprox() && bounds.Position.IsZeroApprox() ? subBounds : bounds.Merge(subBounds);
            } else if (!subBounds.Position.IsZeroApprox()) {
                bounds = bounds.Position.IsZeroApprox() ? subBounds : bounds.Expand(subBounds.Position);
            }
        }

        KnownBounds = bounds;
        if (GetParent() is SceneFolderProxy proxy) {
            proxy.KnownBounds = bounds;
        }
    }

    public override string ToString() => Name;
}