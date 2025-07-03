namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using ReeLib;
using ReeLib.Bvh;

public class McolConverter :
    SceneResourceConverter<MeshColliderResource, McolFile, McolRoot>,
    ISynchronousConverter<MeshColliderResource, McolFile>,
    ISynchronousConverter<McolRoot, McolFile>
{
    public static readonly Color[] LayerColors = [
        Colors.White, Colors.Blue, Colors.Green, Colors.Red, Colors.Magenta,
        Colors.Yellow, Colors.AliceBlue, Colors.AntiqueWhite, Colors.Aqua, Colors.Aquamarine,
        Colors.Beige, Colors.Bisque, Colors.BlanchedAlmond, Colors.BlueViolet, Colors.Brown,
        Colors.Burlywood, Colors.CadetBlue, Colors.Chartreuse, Colors.Chocolate, Colors.Coral,
        Colors.CornflowerBlue, Colors.Cornsilk, Colors.Crimson, Colors.Cyan, Colors.DarkBlue,
        Colors.DarkCyan, Colors.DarkGoldenrod, Colors.DarkGray, Colors.DarkGreen, Colors.DarkKhaki,
        Colors.DarkMagenta, Colors.DarkOliveGreen, Colors.DarkOrange, Colors.DarkOrchid, Colors.DarkRed,
        Colors.DarkSalmon, Colors.DarkSeaGreen
    ];

    private const float MaxPartId = 256;

    public override McolFile CreateFile(FileHandler fileHandler) => new McolFile(fileHandler);

    // public Task<bool> Export(MeshColliderResource source, McolFile file)
    // {
    //     return Task.FromResult(false);
    // }

    public override Task<bool> Import(McolFile file, McolRoot target)
    {
        return Import(file, target.Resource!, target);
    }

    public override Task<bool> Export(McolRoot source, McolFile file)
    {
        if (source.Resource == null) {
            GD.PrintErr("Mcol is missing resource");
            return Task.FromResult(false);
        }
        return Task.FromResult(ExportSync(source, source.Resource, file));
    }

    public bool ExportSync(McolRoot source, MeshColliderResource resource, McolFile file)
    {
        var mesh = resource.GetMesh();
        file.bvh = RebuildBvhFromMesh(mesh, source);
        if (resource.Layers != null) {
            foreach (var layer in resource.Layers) {
                file.bvh.stringTable.Add((layer.MainString ?? string.Empty, string.IsNullOrEmpty(layer.SubString) ? null : layer.SubString));
            }
        }
        return true;
    }

    public bool ImportSync(McolFile file, McolRoot target) => ImportSync(file, target.Resource!);
    public bool ImportSync(McolFile file, MeshColliderResource target)
    {
        var root = target.Instantiate() ?? CreateScenePlaceholder(target).Instantiate<McolRoot>();

        var task = Import(file, target, root);
        if (!task.IsCompleted) return false; // import did not complete synchronously, fail it
        if (!task.Result) return false; // import failed altogether

        if (target.ImportedResource == null || EditorInterface.Singleton.GetEditedSceneRoot() != root) {
            PostImport(target, root);
        }

        return true;
    }

    public async Task<bool> Import(McolFile file, MeshColliderResource target, McolRoot root)
    {
        var colliderRoot = root.ColliderRoot;

        if (file.bvh == null) {
            var meshnode = root.MeshNode;
            if (meshnode != null)
                meshnode.Mesh = null;
            return true;
        }

        var origin = root.GetNodeOrNull<Node3D>("Center");
        if (origin == null) {
            root.AddChild(origin = new Node3D() { Name = "Center" });
            origin.Owner = root;
        }
        origin.Position = file.bvh.ReadBounds().Center.ToGodot();
        colliderRoot.QueueFreeRemoveChildren();

        target.Layers = file.bvh.stringTable.Select((tb, i) => new McolMaterialData() {
            MainString = tb.main,
            SubString = tb.sub,
            Material = new StandardMaterial3D() { ResourceName = LayerToMaterialName(i, tb.main), AlbedoColor = LayerColors[i] },
        }).ToArray();

        if (file.bvh.triangles.Count > 0) {
            Mesh mesh = ImportMesh(file.bvh, target.Layers);
            if (WritesEnabled) {
                // use mesh from the exported gltf instead of the generated one directly
                var meshScene = await ExportToGltf(mesh, root, target, root.Asset!.GetImportFilepathChangeExtension(Config, ".glb")!, false);
                MeshInstance3D newMeshNode;
                if (meshScene != null) {
                    target.Mesh = meshScene;
                    var sceneNode = meshScene.Instantiate<Node3D>(PackedScene.GenEditState.Instance);
                    newMeshNode = sceneNode as MeshInstance3D ?? sceneNode.RequireChildByTypeRecursive<MeshInstance3D>();
                } else {
                    newMeshNode = new MeshInstance3D() { Mesh = mesh };
                    Log("Gltf could not be immediately imported. You may need to reimport the scene");
                }
                newMeshNode.Name = "Mesh";
                var meshnode = root.MeshContainerNode;
                if (meshnode != null) {
                    meshnode.ReplaceBy(newMeshNode);
                    meshnode.QueueFree();
                } else {
                    root.AddChild(newMeshNode);
                }
                newMeshNode.Owner = root;
                meshnode = newMeshNode;
            } else {
                var meshnode = root.MeshNode;
                if (meshnode == null) {
                    root.AddChild(meshnode = new MeshInstance3D() { Name = "Mesh" });
                    meshnode.Owner = root;
                }
                meshnode.Mesh = mesh;
                target.Mesh = meshnode.ToPackedScene(false);
            }
        }
        ImportColliders(file.bvh, colliderRoot);

        target.CachedVertexCount = file.bvh.vertices.Count;

        // we don't really care about the tree since it just gets built off of the mesh and colliders anyway, could be helpful for debugging though
        // GenerateAabbPreviews(file.bvh, root);

        return true;
    }

    private void GenerateAabbPreviews(BvhData bvh, McolRoot root)
    {
        bvh.ReadTree();
        var treeRoot = root.GetNodeOrNull<StaticBody3D>("Tree");
        if (treeRoot == null) {
            root.AddChild(treeRoot = new StaticBody3D() { Name = "Tree" });
            treeRoot.Owner = root;
        }
        treeRoot.QueueFreeRemoveChildren();
        if (bvh.tree?.entries.Count <= 500) {
            int i = 0;
            foreach (var entry in bvh.tree.entries) {
                var isleaf = entry.isLeaf || entry.index == -1;
                if (!isleaf) continue;

                var node = new CollisionShape3D() { Name = ("Bounds" + i++) + "__" + entry.index + "_" + entry.index2 };
                node.Shape = new BoxShape3D() {
                    Size = (entry.boundMin - entry.boundMax).ToGodot()
                };
                node.Position = (entry.boundMin + entry.boundMax).ToGodot() / 2;
                treeRoot.AddChild(node);
                node.Owner = root;
            }
        }
    }

    private const string LayerNameDescSeparator = "___";
    private static string LayerToMaterialName(int index, string description)
    {
        var matname = "Layer" + index + LayerNameDescSeparator + description;
        if (matname.Length > 63) {
            // blender object name limit
            matname = matname.Substr(0, 63);
        }
        return matname;
    }

    private static ArrayMesh ImportMesh(BvhData file, McolMaterialData[] materials)
    {
        ArrayMesh mesh = new ArrayMesh();

        var submeshCount = file.Header.stringCount;
        for (int m = 0; m < submeshCount; m++) {
            var surf = new SurfaceTool();
            surf.Begin(Mesh.PrimitiveType.Triangles);
            var layer = materials[m];
            var mat = layer.Material;
            surf.SetMaterial(mat);
            var verts = file.vertices;
            var faces = file.triangles;

            var dupeTriangles = new Dictionary<int, int>();
            for (int i = 0; i < faces.Count; ++i) {
                var face = faces[i];
                if (face.info.layerIndex == m) {
                    // technically the mask/part are per-face and not per-vertex
                    // but we're splitting each triangle as separate vertices and materials per layer anyway so it doesn't matter
                    surf.SetColor(new Color((uint)(face.info.mask == 0 ? uint.MaxValue : face.info.mask)));
                    surf.SetUV(new Vector2(face.info.partId / MaxPartId, 1));
                    surf.AddVertex(verts[face.posIndex1].ToGodot());
                    surf.SetColor(new Color((uint)(face.info.mask == 0 ? uint.MaxValue : face.info.mask)));
                    surf.SetUV(new Vector2(face.info.partId / MaxPartId, 1));
                    surf.AddVertex(verts[face.posIndex3].ToGodot());
                    surf.SetColor(new Color((uint)(face.info.mask == 0 ? uint.MaxValue : face.info.mask)));
                    surf.SetUV(new Vector2(face.info.partId / MaxPartId, 1));
                    surf.AddVertex(verts[face.posIndex2].ToGodot());
                }
            }

            surf.Index();
            surf.GenerateNormals();
            surf.Commit(mesh);
        }
        return mesh;
    }

    private static void ImportColliders(BvhData file, StaticBody3D colliderRoot)
    {
        int i = 0;
        foreach (var data in file.spheres) {
            var node = new CollisionShape3D() { Name = ColliderData.StringFrom(data.info, "Sphere" + i++) };
            RequestSetCollisionShape3D.ApplyShape(node, data.sphere);
            colliderRoot.AddChild(node);
            node.Owner = colliderRoot.Owner ?? colliderRoot;
        }
        foreach (var data in file.capsules) {
            var node = new CollisionShape3D() { Name = ColliderData.StringFrom(data.info, "Capsule" + i++) };
            RequestSetCollisionShape3D.ApplyShape(node, data.capsule);
            colliderRoot.AddChild(node);
            node.Owner = colliderRoot.Owner ?? colliderRoot;
        }
        foreach (var data in file.boxes) {
            var node = new CollisionShape3D() { Name = ColliderData.StringFrom(data.info, "Box" + i++) };
            RequestSetCollisionShape3D.ApplyShape(node, data.box);
            colliderRoot.AddChild(node);
            node.Owner = colliderRoot.Owner ?? colliderRoot;
        }
    }

    public static Task<PackedScene> ExportToGltf(McolRoot root, string outputPath, bool includeColliders)
    {
        var resource = root.Resource ?? new MeshColliderResource();
        var mesh = root?.MeshNode?.Mesh;
        if (root == null || mesh == null) {
            return Task.FromResult(new PackedScene());
        }

        return ExportToGltf(mesh, root, resource, outputPath, includeColliders);
    }

    private static async Task<PackedScene> ExportToGltf(Mesh mesh, McolRoot root, MeshColliderResource resource, string outputPath, bool includeColliders)
    {
        var doc = new GltfDocument();
        var state = new GltfState();

        var exportMeshInst = new MeshInstance3D() { Name = root.Name, Mesh = mesh };
        doc.AppendFromScene(exportMeshInst, state);
        exportMeshInst.Mesh = null;
        outputPath = ProjectSettings.LocalizePath(outputPath);

        if (includeColliders) {
            var spheresPerLayer = new Dictionary<int, SphereMesh>();
            var layerMats = new Dictionary<int, Material>();
            for (int i = 0; i < mesh.GetSurfaceCount(); i++) {
                var mat = mesh.SurfaceGetMaterial(i);
                var layer = GetLayerIndexFromMaterialName(mat.ResourceName);
                layerMats[layer] = mat;
            }
            for (int i = 0; i < resource.Layers!.Length; i++) {
                var layer = resource.Layers[i];
                if (!layerMats.TryGetValue(i, out var mat)) {
                    layerMats[i] = mat = layer.Material ??= new StandardMaterial3D() { ResourceName = LayerToMaterialName(i, layer.MainString!), AlbedoColor = LayerColors[i] };
                }
                spheresPerLayer[i] = new SphereMesh() { Radius = 0.5f, Height = 1, Rings = 16, RadialSegments = 16, Material = mat };
            }

            var colliders = root.ColliderRoot.GetChildren().OfType<CollisionShape3D>();

            var spheresRoot = new Node3D() { Name = "Spheres" };
            var capsulesRoot = new Node3D() { Name = "Capsules" };
            var boxesRoot = new Node3D() { Name = "Boxes" };
            Node3D spawnedChild;
            foreach (var collider in colliders.OfType<CollisionShape3D>()) {
                var name = collider.Name.ToString();
                var layer = ColliderData.Parse(name).layerIndex;
                var mat = layerMats[layer];
                switch (collider.Shape) {
                    case SphereShape3D sphere:
                        spheresRoot.AddChild(spawnedChild = new MeshInstance3D() {
                            Mesh = spheresPerLayer[layer],
                            Name = name,
                            Transform = new Transform3D(Basis.FromScale(Mathf.Max(0.001f, sphere.Radius) * Vector3.One), collider.Position)
                        });
                        spawnedChild.Owner = spheresRoot;
                        break;
                    case CapsuleShape3D capsule:
                        capsulesRoot.AddChild(spawnedChild = new MeshInstance3D() {
                            Mesh = new CapsuleMesh() { Height = capsule.Height, Radius = capsule.Radius, Rings = 3, RadialSegments = 8, Material = mat },
                            Name = name,
                            Transform = collider.Transform,
                        });
                        spawnedChild.Owner = capsulesRoot;
                        break;
                    case BoxShape3D box:
                        boxesRoot.AddChild(spawnedChild = new MeshInstance3D() {
                            Mesh = new BoxMesh() { Size = box.Size, Material = mat },
                            Name = name,
                            Transform = collider.Transform,
                        });
                        spawnedChild.Owner = boxesRoot;
                        break;
                    default:
                        GD.PrintErr("Unsupported mcol shape type " + collider.Shape.GetType());
                        break;
                }
            }
            doc.AppendFromScene(spheresRoot, state);
            doc.AppendFromScene(capsulesRoot, state);
            doc.AppendFromScene(boxesRoot, state);
        }
        var error = doc.WriteToFilesystem(state, outputPath);
        if (error != Error.Ok) {
            GD.PrintErr("Failed to write mcol gltf: " + error);
            return exportMeshInst.ToPackedScene();
        }

        var result = await ResourceImportHandler.ImportAsset<PackedScene>(outputPath).Await() ?? exportMeshInst.ToPackedScene();
        exportMeshInst.QueueFree();
        return result;
    }

    public BvhData RebuildBvhFromMesh(Mesh? mesh, McolRoot root)
    {
        var bvh = new BvhData(new FileHandler() { FileVersion = PathUtils.GetFileFormatVersion(KnownFileFormats.CollisionMesh, Config) });
        bvh.tree = new BvhTree();

        // mcol shapes need to be added in sorted by type since the bounds contain indexes
        // triangles > spheres > capsules > boxes

        // mcol without a mesh is valid, hence nullable
        if (mesh != null) {
            var surfCount = mesh.GetSurfaceCount();
            var unsetEdgeIndex = Game is SupportedGame.ResidentEvil7 or SupportedGame.DevilMayCry5 or SupportedGame.ResidentEvil2 ? 0 : -1;

            for (int i = 0; i < surfCount; ++i) {
                var surf = mesh.SurfaceGetArrays(i);
                var verts = surf[(int)ArrayMesh.ArrayType.Vertex].AsVector3Array();
                var indices = surf[(int)ArrayMesh.ArrayType.Index].AsInt32Array();
                var uvs = surf[(int)ArrayMesh.ArrayType.TexUV].AsVector2Array();
                var colors = surf[(int)ArrayMesh.ArrayType.Color].AsColorArray();
                var mat = mesh.SurfaceGetMaterial(i);
                var layerIndex = GetLayerIndexFromMaterialName(mat.ResourceName);
                var vertsOffset = bvh.vertices.Count; // this should let us seamlessly handle multi-surface meshes
                foreach (var v in verts) bvh.vertices.Add(v.ToRsz());

                for (int k = 0; k < indices.Length; k += 3) {
                    var vert1 = indices[k];
                    var vert2 = indices[k + 1];
                    var vert3 = indices[k + 2];

                    // storing indices as 1-3-2 and not 1-2-3 because godot and RE winding order is different
                    var indexData = new BvhTriangle() {
                        posIndex1 = vert1 + vertsOffset,
                        posIndex2 = vert3 + vertsOffset,
                        posIndex3 = vert2 + vertsOffset,
                        edgeIndex1 = unsetEdgeIndex,
                        edgeIndex2 = unsetEdgeIndex,
                        edgeIndex3 = unsetEdgeIndex,
                    };
                    // NOTE: edges ignored for now, because I can't get them right and they don't seem to make a difference either
                    indexData.info.mask = colors != null ? colors[vert1].ToRgba32() : uint.MaxValue;
                    indexData.info.layerIndex = layerIndex;
                    indexData.info.partId = Mathf.RoundToInt(uvs[vert1].X * MaxPartId);
                    bvh.AddTriangle(indexData);
                }
            }
        }

        var colliders = root.ColliderRoot.FindChildrenByType<CollisionShape3D>().ToList();

        AddColliders<SphereShape3D> (colliders, bvh, (collider, sphere,  data) => data.ApplyToObjectInfo(bvh.AddCollider(RequestSetCollisionShape3D.ConvertShapeToRsz(collider, sphere)).info));
        AddColliders<CapsuleShape3D>(colliders, bvh, (collider, capsule, data) => data.ApplyToObjectInfo(bvh.AddCollider(RequestSetCollisionShape3D.ConvertShapeToRsz(collider, capsule)).info));
        AddColliders<BoxShape3D>    (colliders, bvh, (collider, box,     data) => data.ApplyToObjectInfo(bvh.AddCollider(RequestSetCollisionShape3D.ConvertShapeToRsz(collider, box)).info));
        static void AddColliders<TShape>(List<CollisionShape3D> colliders, BvhData bvh, Action<Node3D, TShape, ColliderData> func)
        {
            foreach (var collider in colliders) {
                if (collider.Shape is TShape shape) {
                    var name = collider.Name.ToString();
                    var data = ColliderData.Parse(name);
                    func.Invoke(collider, shape, data);
                }
            }
        }

        bvh.BuildTree();
        return bvh;
    }

    private struct ColliderData
    {
        public int layerIndex;
        public uint mask;
        public int partId;

        public void ApplyToObjectInfo(TriangleInfo info)
        {
            info.layerIndex = layerIndex;
            info.mask = mask;
            info.partId = partId;
        }

        public void ApplyToObjectInfo(ObjectInfoUnversioned info)
        {
            info.layerIndex = layerIndex;
            info.mask = mask;
            info.partId = partId;
        }

        public static ColliderData From(ObjectInfoUnversioned info) => new ColliderData() { layerIndex = info.layerIndex, mask = info.mask, partId = info.partId };
        public static string StringFrom(ObjectInfoUnversioned info, string prefix) => new ColliderData() { layerIndex = info.layerIndex, mask = info.mask, partId = info.partId }.BuildString(prefix);

        public string BuildString(string prefix)
        {
            return $"{prefix}__i{layerIndex}__m{mask}__p{partId}";
        }

        public static ColliderData Parse(string name)
        {
            var index = TryGetProp(name, "__i", 0);
            var mask = (uint)TryGetProp(name, "__m", -1);
            var part = TryGetProp(name, "__p", 0);
            return new ColliderData() {
                layerIndex = index == -1 ? 0 : index,
                mask = mask,
                partId = part == -1 ? 0 : -1,
            };
        }

        private static int TryGetProp(string name, string prefix, int defaultValue)
        {
            var start = name.IndexOf(prefix);
            if (start == -1) return defaultValue;
            start = start + prefix.Length;

            var end = start;
            if (name[end] == '-') end++;
            while (end < name.Length && char.IsAsciiDigit(name[end])) {
                end++;
            }
            if (start == end) return defaultValue;

            return int.Parse(name.AsSpan()[start..end]);
        }
    }

    private static int GetLayerIndexFromMaterialName(string name)
    {
        var id = name.IndexOf(LayerNameDescSeparator);
        if (id == -1) {
            throw new Exception("Unsupported mcol material - material name does not meet the Layer##__ expectation");
        }
        return id == -1 ? 0 : int.Parse(name.AsSpan()[5..id]);
    }
}
