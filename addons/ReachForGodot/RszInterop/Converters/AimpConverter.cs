namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using ReeLib;
using ReeLib.Aimp;
using ReeLib.via;

public class AimpConverter : RszAssetConverter<AiMapResource, AimpFile, AiMapResource, AiMapResource>
{
    public override AimpFile CreateFile(FileHandler fileHandler) => new AimpFile(FileOption, fileHandler);

    public override Task<bool> Import(AimpFile file, AiMapResource target)
    {
        target.DefaultAgentRadius = file.Header.agentRadWhenBuild;
        target.mapType = file.Header.mapType;
        target.sectionType = file.Header.sectionType;
        target.name = file.Header.name;
        target.hash = file.Header.hash;
        target.uriHash = file.Header.uriHash;
        target.Layers = file.layers?.Select((l, index) => new MapLayerInfo() {
            Mask = l.flags,
            Name = l.name ?? string.Empty,
            Color = EditorResources.LayerColors[index % 32],
        }).ToArray() ?? Array.Empty<MapLayerInfo>();
        foreach (var rszobj in file.RSZ.ObjectList)
        {
            if (rszobj.RszClass != null && rszobj.RszClass.crc != 0)
            {
                var obj = CreateOrGetObject(rszobj);
                target.Userdata ??= new Godot.Collections.Array<REObject>();
                target.Userdata.Add(obj);
            }
        }

        // no actual import yet - still figuring out if these are importable or we just use the raw files
        return Task.FromResult(false);
    }

    public override Task<bool> Export(AiMapResource source, AimpFile file)
    {
        // ExportNavmesh(source, file);
        return Task.FromResult(false);
    }

    public static ArrayMesh ImportMesh(ContentGroupContainer group, ContentGroupTriangles triangles)
    {
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
        return mesh;
    }

    public static ArrayMesh ImportMesh(ContentGroupContainer group, ContentGroupPolygons polygons)
    {
        Debug.Assert(group?.Positions != null);
        var surf = new SurfaceTool();
        surf.Begin(Mesh.PrimitiveType.Triangles);
        var vertices = group.Positions;

        foreach (var poly in polygons.nodes!) {
            var polyVerts = new Vector3[poly.indices!.Length];
            var p = 0;
            // need to reverse because of winding order
            for (var i = poly.indices!.Length - 1; i >= 0; i--) {
                var pt = poly.indices![i];
                polyVerts[p++] = vertices[pt].Vector3.ToGodot();
            }
            surf.AddTriangleFan(polyVerts);
        }
        var mesh = new ArrayMesh();
        surf.Commit(mesh);
        return mesh;
    }

    public static NavigationMesh GenerateNavmeshFromTriangles(Mesh mesh, float agentRadius)
    {
        var nvm = new NavigationMesh();
        // nvm.CreateFromMesh(mesh);
        var geodata = new NavigationMeshSourceGeometryData3D();
        geodata.AddMesh(mesh, new Transform3D());
        // the manually edited mesh we give it already contains the actual walkable surface
        // so we need to make sure we don't lose any of it
        // border size = 0 should probably handle it?
        // or maybe set AgentRadius = 0?
        nvm.AgentRadius = agentRadius;
        nvm.BorderSize = 0;
        // There's an BakeFromSourceGeometryDataAsync method, so I assume this'll just finish synchronously?
        NavigationServer3D.BakeFromSourceGeometryData(nvm, geodata);
        return nvm;
    }

    public static void ExportNavmesh(Mesh sourceMesh, NavigationMesh navmesh, AimpFile file)
    {
        var container1 = (file.mainContent ??= new ContentGroupContainer(file.Header.Version));
        var positions1 = new List<PaddedVec3>();

        var triangles = new ContentGroupTriangles();
        var trilist = new List<TriangleNode>();
        var nodes = new List<NodeInfo>();
        var surfcount = sourceMesh.GetSurfaceCount();
        for (int i = 0; i < surfcount; ++i) {
            var surf = sourceMesh.SurfaceGetArrays(i);
            var trinode = new TriangleNode();
            var verts = surf[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var indices = surf[(int)Mesh.ArrayType.Index].AsInt32Array();
            foreach (var pt in verts) {
                positions1.Add(new PaddedVec3(pt.X, pt.Y, pt.Z));
            }
            for (var ind = 0; ind < indices.Length; ind+= 3) {
                var pt = indices[ind];

                trinode.index1 = indices[ind + 0];
                trinode.index2 = indices[ind + 2];
                trinode.index3 = indices[ind + 1];
            }
            // TODO figure out vertex attribute IDs and values
            trinode.attributes ??= new();
            trinode.attributes.Init(3);
            trilist.Add(trinode);
            var node = new NodeInfo();
            node.index = nodes.Count;
            node.groupIndex = 0;
            // TODO figure out node attribute IDs and values
            // node.attributes
            // node.flags
            // TODO how are we storing per-node userdata?
            // node.userdataIndex = 0;
            nodes.Add(node);
            node.nextIndex = nodes.Count;
        }
        triangles.nodes = trilist.ToArray();
        triangles.polygonIndices = new int[triangles.nodes.Length];
        // TODO handle MapBoundary
        // TODO handle ContentGroupWall
        container1.Positions = positions1.ToArray();
        container1.Nodes ??= new ();
        container1.Nodes.maxIndex = 1;
        // TODO figure out QuadData
        // container1.QuadData
        // TODO how do we handle node links?
        // container1.Nodes.Links
        container1.Nodes.minIndex = 0;
        container1.Nodes.maxIndex = nodes.Count - 1;

        var container2 = (file.secondaryContent ??= new ContentGroupContainer(file.Header.Version));
        var positions2 = new List<PaddedVec3>();

        var polygons = new ContentGroupPolygons();
        var polycount = navmesh.GetPolygonCount();
        polygons.nodes = new PolygonNode[polycount];
        polygons.triangleIndices = new IndexSet[polycount];
        var polyVerts = navmesh.GetVertices();
        foreach (var vec in polyVerts) {
            positions2.Add(new PaddedVec3(vec.X, vec.Y, vec.Z));
        }

        nodes.Clear();
        for (int i = 0; i < polycount; ++i) {
            var polyNode = new PolygonNode();
            var node = new NodeInfo();
            var polyIndices = navmesh.GetPolygon(i);

            polyNode.pointCount = polyIndices.Length;
            polyNode.indices = new int[polyNode.pointCount];
            AABB bounds = AABB.MaxMin;
            for (int k = 0; k < polyNode.pointCount; ++k) {
                polyNode.indices[k] = polyIndices[polyNode.pointCount - 1 - k];
                var vec = polyVerts[polyNode.indices[k]];
                bounds.Extend(vec.ToRsz());
            }
            polyNode.min = bounds.minpos;
            polyNode.max = bounds.maxpos;
            // TODO figure out vertex attribute IDs and values
            polyNode.attributes ??= new();
            polyNode.attributes.Init(polyNode.pointCount);
            polygons.nodes[i] = polyNode;

            polygons.triangleIndices[i] = new IndexSet();

            node.index = nodes.Count;
            node.groupIndex = 0;
            // node.attributes
            // node.flags
            // node.userdataIndex = 0;
            nodes.Add(node);
            node.nextIndex = nodes.Count;
        }

        // TODO handle ContentGroupAABB

        // TODO figure out how to handle linking triangles and polygons to their counterpart geometry
        // maybe use the polygon AABB to find all triangles that are fully contained in the polygon?
        // hopefully there's no multiple overlap, or maybe it's acceptable
        // maybe partial AABB containment is enough and overlapping is expected
        // maybe start with triangles and for each, find the most contained polygon (closest to center maybe), then link back from polygons
        // polygons.triangleIndices
        // triangles.polygonIndices

        container2.Positions = positions2.ToArray();
        container2.contents ??= new ContentGroup[1];
        container2.contents[0] = polygons;
        // TODO embedded navmeshes
    }
}
