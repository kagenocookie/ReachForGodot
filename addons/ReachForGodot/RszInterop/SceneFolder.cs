namespace RGE;

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool]
public partial class SceneFolder : Node, IRszContainerNode
{
    [Export] public SupportedGame Game { get; set; }
    [Export] public AssetReference? Asset { get; set; }
    [Export] public REResource[]? Resources { get; set; }
    [Export] public Node? FolderContainer { get; private set; }
    [Export] public Aabb KnownBounds { get; set; }

    public bool IsEmpty => GetChildCount() == 0;

    public IEnumerable<SceneFolder> Subfolders => FolderContainer?.FindChildrenByType<SceneFolder>() ?? Array.Empty<SceneFolder>();
    public IEnumerable<SceneFolder> AllSubfolders => Subfolders.SelectMany(f => new [] { f }.Concat(f.AllSubfolders));

    public void AddFolder(SceneFolder folder)
    {
        if ((FolderContainer ??= FindChild("Folders")) == null) {
            AddChild(FolderContainer = new Node() { Name = "Folders" });
            FolderContainer.Owner = this;
            MoveChild(FolderContainer, 0);
        }
        FolderContainer.AddChild(folder);
        folder.Owner = Owner ?? this;
    }

    public void RemoveFolder(SceneFolder folder)
    {
        if (FolderContainer != null && folder.GetParent() == FolderContainer) {
            FolderContainer.RemoveChild(folder);
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

    public void Clear()
    {
        this.ClearChildren();
        FolderContainer = null;
        Resources = Array.Empty<REResource>();
    }

    public SceneFolder? GetFolder(string name)
    {
        return FolderContainer?.FindChildWhere<SceneFolder>(c => c.Name == name);
    }

    public REGameObject? GetTopLevelGameObject(string name, int deduplicationIndex)
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
        var conv = new RszGodotConverter(ReachForGodot.GetAssetConfig(Game!)!, options);
        conv.RegenerateSceneTree(this).ContinueWith((t) => {
            if (t.IsFaulted) {
                GD.Print("Tree rebuild failed:", t.Exception);
            } else {
                GD.Print("Tree rebuild finished in " + sw.Elapsed);
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