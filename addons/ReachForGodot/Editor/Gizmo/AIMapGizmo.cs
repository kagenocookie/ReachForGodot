using Godot;
using ReeLib;
using ReeLib.Aimp;

namespace ReaGE;

public partial class AIMapGizmo : EditorNode3DGizmoPlugin
{
    public override string _GetGizmoName()
    {
        return "AI Map preview";
    }

    public override bool _HasGizmo(Node3D forNode3D)
    {
        if (forNode3D is GameObject gameObject) {
            return gameObject.Components?.OfType<IAIMapComponent>().Any() == true;
        }
        return false;
    }

    // public override EditorNode3DGizmo _CreateGizmo(Node3D forNode3D)
    // {
    //     if (forNode3D is not GameObject gameObject) {
    //         return default!;
    //     }
    //     var mapfile = gameObject.GetComponent<IAIMapComponent>()?.File;
    //     if (mapfile == null) return default!;

    //         // var gizmo = new EditorNode3DGizmo();
    //         // gizmo.SetNode3D(forNode3D);
    //         // return gizmo;


    //     return base._CreateGizmo(forNode3D);
    // }

    public override void _Redraw(EditorNode3DGizmo gizmo)
    {
        if (gizmo.GetNode3D() is not GameObject gameObject) {
            return;
        }

        gizmo.Clear();

        var comp = gameObject.GetComponent<IAIMapComponent>();
        var map = comp?.File;
        if (comp == null || map == null) return;

        if (map.mainContent?.contents != null && comp.PreviewMainGroup) RedrawContents(gizmo, map.mainContent, EditorResources.NavmeshMaterial1);
        if (map.secondaryContent?.contents != null && comp.PreviewSecondGroup) RedrawContents(gizmo, map.secondaryContent, EditorResources.NavmeshMaterial2);
    }

    private void RedrawContents(EditorNode3DGizmo gizmo, ContentGroupContainer group, Material material)
    {
        foreach (var content in group.contents!) {
            if (content is ContentGroupTriangles triangles) {
                var mesh = AimpConverter.ImportMesh(group, triangles);
                gizmo.AddMesh(mesh, material);
            } else if (content is ContentGroupPolygons polygons) {
                var mesh = AimpConverter.ImportMesh(group, polygons);
                gizmo.AddMesh(mesh, material);
            }
        }
    }
}

public interface IAIMapComponent
{
    REResource? MapResource { get; }
    AimpFile? File { get; }
    bool PreviewMainGroup { get; }
    bool PreviewSecondGroup { get; }
}