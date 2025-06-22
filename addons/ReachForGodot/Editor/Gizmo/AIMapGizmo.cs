using Godot;
using RszTool;
using RszTool.Aimp;

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
                Debug.Assert(group?.Positions != null);
                // var linePoints = new Vector3[triangles];
                var surf = new SurfaceTool();
                surf.Begin(Mesh.PrimitiveType.Triangles);
                var vertices = group.Positions;
                foreach (var tri in triangles.nodes!) {
                    surf.AddVertex(vertices[tri.index1].Vector3.ToGodot());
                    surf.AddVertex(vertices[tri.index3].Vector3.ToGodot());
                    surf.AddVertex(vertices[tri.index2].Vector3.ToGodot());
                }
                var mesh = new ArrayMesh();
                surf.Commit(mesh);
                gizmo.AddMesh(mesh, material);
            } else if (content is ContentGroupPolygons polygons) {
                Debug.Assert(group?.Positions != null);
                var surf = new SurfaceTool();
                surf.Begin(Mesh.PrimitiveType.Triangles);
                var vertices = group.Positions;

                foreach (var poly in polygons.nodes!) {
                    // var polyVerts = new Vector3[poly.indices!.Length + 1];
                    // polyVerts[0] = ((poly.min + poly.max) / 2).ToGodot();
                    // var p = 1;
                    // for (var i = poly.indices!.Length - 1; i >= 0; i--) {
                    //     var pt = poly.indices![i];
                    //     polyVerts[p++] = vertices[pt].Vector3.ToGodot();
                    // }
                    var polyVerts = new Vector3[poly.indices!.Length];
                    var p = 0;
                    for (var i = poly.indices!.Length - 1; i >= 0; i--) {
                        var pt = poly.indices![i];
                        polyVerts[p++] = vertices[pt].Vector3.ToGodot();
                    }
                    surf.AddTriangleFan(polyVerts);
                    // surf.AddVertex()
                    // poly.indices
                    // surf.AddVertex(vertices[tri.index1].Vector3.ToGodot());
                    // surf.AddVertex(vertices[tri.index3].Vector3.ToGodot());
                    // surf.AddVertex(vertices[tri.index2].Vector3.ToGodot());
                }
                var mesh = new ArrayMesh();
                surf.Commit(mesh);
                gizmo.AddMesh(mesh, material);
            } else if (content is ContentGroupPoints points) {

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