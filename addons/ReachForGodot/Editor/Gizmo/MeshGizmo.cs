#if TOOLS
using Godot;

namespace ReaGE;

public partial class MeshGizmo : EditorNode3DGizmoPlugin
{
    public override string _GetGizmoName()
    {
        return "Component mesh preview";
    }

    public override EditorNode3DGizmo _CreateGizmo(Node3D forNode3D)
    {
        if (forNode3D is not GameObject gameObject) {
            return default!;
        }

        var gizmoMesh = gameObject.Components?.OfType<IGizmoMeshProvider>().FirstOrDefault()?.GetMeshFilepath();
        if (string.IsNullOrEmpty(gizmoMesh)) return default!;

        var resource = Importer.FindOrImportResource<MeshResource>(gizmoMesh, ReachForGodot.GetAssetConfig(gameObject.Game));
        if (resource == null) return default!;

        if (resource.ImportedMesh == null) {
            resource.Import(false);
        } else {
            var gizmo = new EditorNode3DGizmo();
            gizmo.SetNode3D(forNode3D);
            return gizmo;
            // var inst = resource.ImportedMesh.Instantiate<Node>();
            // var meshes = inst?.FindChildrenByTypeRecursive<MeshInstance3D>().Select(m => m.Mesh);
            // if (meshes?.Any() == true) {
            //     var gizmo = new EditorNode3DGizmo();
            //     gizmo.SetNode3D(forNode3D);
            //     foreach (var mesh in meshes) {
            //         gizmo.AddMesh(mesh);
            //     }
            //     return gizmo;
            // }
        }

        return base._CreateGizmo(forNode3D);
    }

    public override void _Redraw(EditorNode3DGizmo gizmo)
    {
        if (gizmo.GetNode3D() is not GameObject gameObject) {
            return;
        }

        var gizmoMesh = gameObject.Components?.OfType<IGizmoMeshProvider>().FirstOrDefault()?.GetMeshFilepath();
        if (string.IsNullOrEmpty(gizmoMesh)) return;

        var resource = Importer.FindOrImportResource<MeshResource>(gizmoMesh, ReachForGodot.GetAssetConfig(gameObject.Game));
        if (resource == null) return;

        if (resource.ImportedMesh == null) {
            resource.Import(false).ContinueWith(_ => CallDeferred(MethodName._Redraw, gizmo));
            return;
        }

        var inst = resource.ImportedMesh.Instantiate<Node>();
        var meshes = inst?.FindChildrenByTypeRecursive<MeshInstance3D>().Select(m => m.Mesh);
        if (meshes?.Any() == true) {
            foreach (var mesh in meshes) {
                gizmo.AddMesh(mesh);
            }
            // gizmo.AddHandles()
        }
    }

    public override bool _HasGizmo(Node3D forNode3D)
    {
        if (forNode3D is GameObject gameObject) {
            var gizmoComps = gameObject.Components?.OfType<IGizmoMeshProvider>().FirstOrDefault();
            return gizmoComps != null;
        }
        return false;
    }
}

public interface IGizmoMeshProvider
{
    string? GetMeshFilepath();
}

#endif
