namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using RszTool;
using RszTool.Bvh;
using Shouldly;

public class McolConverter :
    SceneResourceConverter<MeshColliderResource, McolFile, McolRoot>,
    ISynchronousConverter<MeshColliderResource, McolFile>,
    ISynchronousConverter<McolRoot, McolFile>
{
    private static readonly Color[] McolLayerColors = [
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

    public bool ImportFromFile(string sourcePath, McolRoot? imported, MeshColliderResource? resource)
    {
        Clear();
        var file = CreateFile(new FileHandler(sourcePath));
        if (!LoadFile(file)) return false;
        resource ??= imported?.Resource ?? Importer.FindOrImportResource<MeshColliderResource>(sourcePath, Config, WritesEnabled);
        if (resource == null) {
            GD.PrintErr("Resource could not be created: " + sourcePath);
            return false;
        }
        if (imported == null) {
            resource.ImportedResource ??= Importer.FindOrImportAsset<PackedScene>(resource.Asset!.AssetFilename, Config, WritesEnabled);
            // resource.ImportedResource ??= Importer.FindOrImportAsset<PackedScene>(sourcePath, Config, WritesEnabled);
            imported = resource.Instantiate();
            if (imported == null) return false;
        }

        if (!ImportSync(file, resource, imported)) return false;
        // if (!await Import(file, imported)) return false;
        // TODO save imported resource
        Clear();
        return true;
    }

    public Task<bool> Import(McolFile file, MeshColliderResource target)
    {
        return Task.FromResult(ImportSync(file, target, target.Instantiate() ?? CreateScenePlaceholder(target).Instantiate<McolRoot>()));
    }

    // public Task<bool> Export(MeshColliderResource source, McolFile file)
    // {
    //     return Task.FromResult(false);
    // }

    public override Task<bool> Import(McolFile file, McolRoot target)
    {
        return Task.FromResult(ImportSync(file, target.Resource!, target));
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
        var mesh = source.MeshNode!.Mesh;
        file.bvh = RebuildBvhFromMesh(mesh, source);
        if (resource.Layers != null) {
            foreach (var layer in resource.Layers) {
                file.bvh.stringTable.Add((layer.MainString ?? string.Empty, layer.SubString));
            }
        }
        return true;
    }

    public bool ImportSync(McolFile file, McolRoot target) => ImportSync(file, target.Resource!);
    public bool ImportSync(McolFile file, MeshColliderResource target)
    {
        var root = target.Instantiate() ?? CreateScenePlaceholder(target).Instantiate<McolRoot>();

        if (!ImportSync(file, target, root)) return false;
        if (target.ImportedResource == null || EditorInterface.Singleton.GetEditedSceneRoot() != root) {
            PostImport(target, root);
        }

        return true;
    }

    public bool ImportSync(McolFile file, MeshColliderResource target, McolRoot root)
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
        colliderRoot.FreeAllChildrenImmediately();

        Mesh mesh = ImportMesh(file.bvh);
        if (WritesEnabled) {
            // use mesh from the exported gltf instead of the generated one directly
            var meshScene = ExportToGltf(mesh, root, target, root.Asset!.GetImportFilepathChangeExtension(Config, ".gltf")!, false);
            target.Mesh = meshScene;
            var newMeshNode = meshScene.Instantiate<MeshInstance3D>(PackedScene.GenEditState.Instance);
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
        ImportColliders(file.bvh, colliderRoot);

        target.Layers = file.bvh.stringTable.Select((tb, i) => new McolMaterialData() {
            MainString = tb.main,
            SubString = tb.sub,
            Material = new StandardMaterial3D() { ResourceName = LayerToMaterialName(i, tb.main), AlbedoColor = McolLayerColors[i] },
        }).ToArray();

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
        treeRoot.FreeAllChildrenImmediately();
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

    private static ArrayMesh ImportMesh(BvhData file)
    {
        ArrayMesh mesh = new ArrayMesh();

        var submeshCount = file.Header.stringCount;
        for (int m = 0; m < submeshCount; m++) {
            var surf = new SurfaceTool();
            surf.Begin(Mesh.PrimitiveType.Triangles);
            var matname = LayerToMaterialName(m, file.stringTable[m].main);
            var mat = new StandardMaterial3D() { ResourceName = matname };
            mat.AlbedoColor = new Color(McolLayerColors[m]);
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

    public static PackedScene ExportToGltf(McolRoot root, string outputPath, bool includeColliders)
    {
        var resource = root.Resource ?? new MeshColliderResource();
        var mesh = root?.MeshNode?.Mesh;
        if (root == null || mesh == null) {
            return new PackedScene();
        }

        return ExportToGltf(mesh, root, resource, outputPath, includeColliders);
    }

    private static PackedScene ExportToGltf(Mesh mesh, McolRoot root, MeshColliderResource resource, string outputPath, bool includeColliders)
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
                    layerMats[i] = mat = layer.Material ??= new StandardMaterial3D() { ResourceName = LayerToMaterialName(i, layer.MainString!), AlbedoColor = McolLayerColors[i] };
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
        doc.WriteToFilesystem(state, outputPath);
        exportMeshInst.QueueFree();

        if (ResourceLoader.Exists(outputPath)) {
            EditorInterface.Singleton.GetResourceFilesystem().ReimportFiles([outputPath]);
        } else {
            EditorInterface.Singleton.GetResourceFilesystem().UpdateFile(outputPath);
            EditorInterface.Singleton.GetResourceFilesystem().ReimportFiles([outputPath]);
        }
        return ResourceLoader.Load<PackedScene>(outputPath);
    }

    public BvhData RebuildBvhFromMesh(Mesh mesh, McolRoot root)
    {
        var bvh = new BvhData(new FileHandler() { FileVersion = PathUtils.GetFileFormatVersion(SupportedFileFormats.MeshCollider, Config.Paths) });
        bvh.tree = new BvhTree();

        foreach (var collider in root.ColliderRoot.FindChildrenByType<CollisionShape3D>()) {
            var name = collider.Name.ToString();
            var data = ColliderData.Parse(name);
            switch (collider.Shape) {
                case SphereShape3D sphere:
                    data.ApplyToObjectInfo(bvh.AddCollider(RequestSetCollisionShape3D.ConvertShapeToRsz(collider, sphere)).info);
                    break;
                case CapsuleShape3D capsule:
                    data.ApplyToObjectInfo(bvh.AddCollider(RequestSetCollisionShape3D.ConvertShapeToRsz(collider, capsule)).info);
                    break;
                case BoxShape3D box:
                    data.ApplyToObjectInfo(bvh.AddCollider(RequestSetCollisionShape3D.ConvertShapeToRsz(collider, box)).info);
                    break;
                default:
                    GD.PrintErr("Unsupported mcol shape type " + collider.Shape.GetType());
                    break;
            }
        }

        var surfCount = mesh.GetSurfaceCount();

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
                var vi_1 = indices[k] + vertsOffset;
                var vi_2 = indices[k + 1] + vertsOffset;
                var vi_3 = indices[k + 2] + vertsOffset;

                var indexData = new BvhTriangle() {
                    posIndex1 = vi_1,
                    posIndex2 = vi_2,
                    posIndex3 = vi_3,
                    edgeIndex1 = -1,
                    edgeIndex2 = -1,
                    edgeIndex3 = -1,
                };
                // NOTE: edges ignored for now, because I can't get them right and they don't seem to make a difference either
                indexData.info.mask = colors != null ? colors[vi_1].ToRgba32() : uint.MaxValue;
                indexData.info.layerIndex = layerIndex;
                indexData.info.partId = Mathf.RoundToInt(uvs[vi_1].X * MaxPartId);
                bvh.AddTriangle(indexData);
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
