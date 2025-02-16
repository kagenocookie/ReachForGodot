namespace RFG;

using System;
using System.Diagnostics;
using System.Runtime.Loader;
using Godot;
using RszTool;

public class GodotScnConverter : IDisposable
{
    private static bool hasSafetyHooked;

    private static readonly Dictionary<string, Func<RszContainerNode, REGameObject, RszInstance, Node?>> factories = new();

    public AssetConfig AssetConfig { get; }
    public bool FullImport { get; }
    public ScnFile? ScnFile { get; private set; }
    public PfbFile? PfbFile { get; private set; }

    public static void EnsureSafeJsonLoadContext()
    {
        if (!hasSafetyHooked && Engine.IsEditorHint()) {
            hasSafetyHooked = true;
            AssemblyLoadContext.GetLoadContext(typeof(GodotScnConverter).Assembly)!.Unloading += (c) => {
                var assembly = typeof(System.Text.Json.JsonSerializerOptions).Assembly;
                var updateHandlerType = assembly.GetType("System.Text.Json.JsonSerializerOptionsUpdateHandler");
                var clearCacheMethod = updateHandlerType?.GetMethod("ClearCache", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                clearCacheMethod!.Invoke(null, new object?[] { null });
                factories.Clear();
            };
        }
    }

    static GodotScnConverter()
    {
        ComponentTypes.Init();
    }

    public GodotScnConverter(AssetConfig paths, bool fullImport)
    {
        AssetConfig = paths;
        FullImport = fullImport;
    }

    public void CreateProxyScene(string sourceScnFilePath, string importFilepath)
    {
        var relativeSourceFile = string.IsNullOrEmpty(AssetConfig.Paths.ChunkPath) ? sourceScnFilePath : sourceScnFilePath.Replace(AssetConfig.Paths.ChunkPath, "");

        Directory.CreateDirectory(ProjectSettings.GlobalizePath(importFilepath.GetBaseDir()));

        var name = sourceScnFilePath.GetFile().GetBaseName().GetBaseName();
        // opening the scene file mostly just to verify that it's valid
        using var scn = OpenScn(sourceScnFilePath);
        scn.Read();

        if (!ResourceLoader.Exists(importFilepath)) {
            var scene = new PackedScene();
            scene.Pack(new SceneFolder() { Game = AssetConfig.Game, Name = name, Asset = new AssetReference() { AssetFilename = relativeSourceFile } });
            ResourceSaver.Save(scene, importFilepath);
        }
    }

    public void CreateProxyPrefab(string sourceScnFilePath, string importFilepath)
    {
        var relativeSourceFile = string.IsNullOrEmpty(AssetConfig.Paths.ChunkPath) ? sourceScnFilePath : sourceScnFilePath.Replace(AssetConfig.Paths.ChunkPath, "");

        Directory.CreateDirectory(ProjectSettings.GlobalizePath(importFilepath.GetBaseDir()));

        var name = sourceScnFilePath.GetFile().GetBaseName().GetBaseName();
        // opening the scene file mostly just to verify that it's valid
        using var file = OpenPfb(sourceScnFilePath);
        file.Read();

        if (!ResourceLoader.Exists(importFilepath)) {
            var scene = new PackedScene();
            scene.Pack(new PrefabNode() { Game = AssetConfig.Game, Name = name, Asset = new AssetReference() { AssetFilename = relativeSourceFile } });
            ResourceSaver.Save(scene, importFilepath);
        }
    }

    public void GenerateSceneTree(SceneFolder root)
    {
        var scnFullPath = Importer.ResolveSourceFilePath(root.Asset!.AssetFilename, AssetConfig);

        ScnFile?.Dispose();
        GD.Print("Opening scn file " + scnFullPath);
        ScnFile = OpenScn(scnFullPath);
        ScnFile.Read();
        ScnFile.SetupGameObjects();

        root.Clear();

        foreach (var folder in ScnFile.FolderDatas!.OrderBy(o => o.Instance!.Index)) {
            Debug.Assert(folder.Info != null);
            GenerateFolder(root, folder);
        }

        GenerateResources(root, ScnFile.ResourceInfoList, AssetConfig);

        foreach (var gameObj in ScnFile.GameObjectDatas!.OrderBy(o => o.Instance!.Index)) {
            Debug.Assert(gameObj.Info != null);
            GenerateGameObject(root, gameObj);
        }
    }

    public void GeneratePrefabTree(PrefabNode root)
    {
        var scnFullPath = Importer.ResolveSourceFilePath(root.Asset!.AssetFilename, AssetConfig);

        PfbFile?.Dispose();
        GD.Print("Opening scn file " + scnFullPath);
        PfbFile = OpenPfb(scnFullPath);
        PfbFile.Read();
        PfbFile.SetupGameObjects();

        root.Clear();

        GenerateResources(root, PfbFile.ResourceInfoList, AssetConfig);

        foreach (var gameObj in PfbFile.GameObjectDatas!.OrderBy(o => o.Instance!.Index)) {
            Debug.Assert(gameObj.Info != null);
            GenerateGameObject(root, gameObj);
        }
    }

    private void GenerateResources(RszContainerNode root, List<ResourceInfo> resourceInfos, AssetConfig config)
    {
        var resources = new List<REResource>();
        foreach (var res in resourceInfos) {
            if (res.Path != null) {
                var format = Importer.GetFileFormat(res.Path);
                var newres = new REResource() {
                    SourcePath = res.Path,
                    ResourceType = format.format,
                    ResourceName = res.Path.GetFile()
                };
                resources.Add(newres);
                if (format.format == RESupportedFileFormats.Unknown) {
                    continue;
                }

                newres.ImportedPath = Importer.GetDefaultImportPath(res.Path, format, config);
                if (ResourceLoader.Exists(newres.ImportedPath)) {
                    newres.ImportedResource = ResourceLoader.Load(newres.ImportedPath);
                    continue;
                }

                if (format.version == -1) {
                    format.version = Importer.GuessFileVersion(res.Path, format.format, config);
                }
                var sourceWithVersion = Importer.ResolveSourceFilePath(res.Path, config);
                // var sourceWithVersion = Path.Join(config.Paths.ChunkPath, newres.SourcePath + "." + format.version).Replace('\\', '/');
                if (!File.Exists(sourceWithVersion)) {
                    GD.Print("Resource not found: " + sourceWithVersion);
                    continue;
                }
                switch (newres.ResourceType) {
                    case RESupportedFileFormats.Mesh:
                        Importer.ImportMesh(sourceWithVersion, ProjectSettings.GlobalizePath(newres.ImportedPath)).Wait();
                        // Importer.Import(format, sourceWithVersion, ProjectSettings.GlobalizePath(newres.ImportedPath), config).Wait();
                        break;
                }
            } else {
                GD.Print("Found a resource with null path: " + resources.Count);
            }
        }
        root.Resources = resources.ToArray();
    }

    private void GenerateFolder(SceneFolder root, ScnFile.FolderData folder, SceneFolder? parent = null)
    {
        Debug.Assert(folder.Info != null);
        SceneFolder newFolder;
        if (folder.Instance?.GetFieldValue("v5") is string scnPath && !string.IsNullOrWhiteSpace(scnPath)) {
            var importPath = Importer.GetDefaultImportPath(scnPath, AssetConfig);
            PackedScene scene;
            if (!ResourceLoader.Exists(importPath)) {
                Importer.ImportScene(Importer.ResolveSourceFilePath(scnPath, AssetConfig), importPath, AssetConfig);
                scene = ResourceLoader.Load<PackedScene>(importPath);
                newFolder = scene.Instantiate<SceneFolder>(PackedScene.GenEditState.Instance);
            } else {
                scene = ResourceLoader.Load<PackedScene>(importPath);
                newFolder = scene.Instantiate<SceneFolder>(PackedScene.GenEditState.Instance);
            }

            if (FullImport && newFolder.IsEmpty) {
                using var childConf = new GodotScnConverter(AssetConfig, FullImport);
                childConf.GenerateSceneTree(newFolder);
                scene.Pack(newFolder);
                ResourceSaver.Save(scene);
            }

            (parent ?? root).AddFolder(newFolder);
        } else {
            newFolder = new SceneFolder() {
                ObjectId = folder.Info.Data.objectId,
                Game = root.Game,
                Name = folder.Name ?? "UnnamedFolder"
            };
            (parent ?? root).AddFolder(newFolder);
        }

        foreach (var child in folder.Children) {
            GenerateFolder(root, child, newFolder);
        }
    }

    private void GenerateGameObject(RszContainerNode root, PfbFile.GameObjectData data, REGameObject? parent = null)
    {
        Debug.Assert(data.Info != null);

        var newGameobj = new REGameObject() {
            ObjectId = data.Info.Data.objectId,
            Name = data.Name ?? "UnnamedGameobject",
            Uuid = Guid.NewGuid().ToString(),
            Enabled = true, // TODO which gameobject field is enabled?
            // Enabled = gameObj.Instance.GetFieldValue("v2")
        };
        root.AddGameObject(newGameobj, parent);

        foreach (var child in data.Children.OrderBy(o => o.Instance!.Index)) {
            GenerateGameObject(root, child, newGameobj);
        }

        var meshComponent = data.Components.FirstOrDefault(c => c.RszClass.name == "via.render.Mesh" || c.RszClass.name == "via.render.CompositeMesh");
        if (meshComponent != null) {
            newGameobj.Node3D = SetupComponent(root, meshComponent, newGameobj) as Node3D;
        }

        foreach (var comp in data.Components.OrderBy(o => o.Index)) {
            if (comp != meshComponent) {
                SetupComponent(root, comp, newGameobj);
            }
        }
    }

    private void GenerateGameObject(RszContainerNode root, ScnFile.GameObjectData data, REGameObject? parent = null)
    {
        Debug.Assert(data.Info != null);

        var newGameobj = new REGameObject() {
            ObjectId = data.Info.Data.objectId,
            Name = data.Name ?? "UnnamedFolder",
            Uuid = data.Info.Data.guid.ToString(),
            Prefab = data.Prefab?.Path,
            Enabled = true, // TODO which gameobject field is enabled?
            // Enabled = gameObj.Instance.GetFieldValue("v2")
        };
        root.AddGameObject(newGameobj, parent);

        foreach (var child in data.Children.OrderBy(o => o.Instance!.Index)) {
            GenerateGameObject(root, child, newGameobj);
        }

        var meshComponent = data.Components.FirstOrDefault(c => c.RszClass.name == "via.render.Mesh" || c.RszClass.name == "via.render.CompositeMesh");
        if (meshComponent != null) {
            newGameobj.Node3D = SetupComponent(root, meshComponent, newGameobj) as Node3D;
        }

        foreach (var comp in data.Components.OrderBy(o => o.Index)) {
            if (comp != meshComponent) {
                SetupComponent(root, comp, newGameobj);
            }
        }
    }

    private ScnFile OpenScn(string filename)
    {
        EnsureSafeJsonLoadContext();
        return new ScnFile(new RszFileOption(AssetConfig.Paths.GetRszToolGameEnum(), AssetConfig.Paths.RszJsonPath ?? throw new Exception("Rsz json file not specified for game " + AssetConfig.Game)), new FileHandler(filename));
    }

    private PfbFile OpenPfb(string filename)
    {
        EnsureSafeJsonLoadContext();
        return new PfbFile(new RszFileOption(AssetConfig.Paths.GetRszToolGameEnum(), AssetConfig.Paths.RszJsonPath ?? throw new Exception("Rsz json file not specified for game " + AssetConfig.Game)), new FileHandler(filename));
    }

    private Node SetupComponent(RszContainerNode root, RszInstance instance, REGameObject gameObject)
    {
        REComponent? componentInfo;
        Node? child;
        if (factories.TryGetValue(instance.RszClass.name, out var factory)) {
            child = factory.Invoke(root, gameObject, instance);
            componentInfo = child as REComponent;
            if (componentInfo != null) {
                child = componentInfo;
            } else if (child != null) {
                child.AddOwnedChild(componentInfo = new REComponent() { Name = "ComponentInfo" });
            } else {
                child = componentInfo = gameObject.AddOwnedChild(new REComponent() { Name = instance.RszClass.name });
            }
        } else {
            child = componentInfo = gameObject.AddOwnedChild(new REComponent() { Name = instance.RszClass.name });
        }

        componentInfo.Classname = instance.RszClass.name;
        componentInfo.ObjectId = instance.Index;
        return child;
    }

    public static void DefineComponentFactory(string componentType, Func<RszContainerNode, REGameObject, RszInstance, Node?> factory)
    {
        factories[componentType] = factory;
    }

    public void Dispose()
    {
        ScnFile?.Dispose();
        GC.SuppressFinalize(this);
    }
}
