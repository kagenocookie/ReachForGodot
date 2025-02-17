namespace RFG;

using System;
using System.Diagnostics;
using System.Runtime.Loader;
using Godot;
using RszTool;

public class RszGodotConverter : IDisposable
{
    private static bool hasSafetyHooked;

    private static readonly Dictionary<SupportedGame, Dictionary<string, Func<IRszContainerNode, REGameObject, RszInstance, REComponent?>>> perGameFactories = new();

    public AssetConfig AssetConfig { get; }
    public bool FullImport { get; }
    public ScnFile? ScnFile { get; private set; }
    public PfbFile? PfbFile { get; private set; }

    public static void EnsureSafeJsonLoadContext()
    {
        if (!hasSafetyHooked && Engine.IsEditorHint()) {
            hasSafetyHooked = true;
            AssemblyLoadContext.GetLoadContext(typeof(RszGodotConverter).Assembly)!.Unloading += (c) => {
                var assembly = typeof(System.Text.Json.JsonSerializerOptions).Assembly;
                var updateHandlerType = assembly.GetType("System.Text.Json.JsonSerializerOptionsUpdateHandler");
                var clearCacheMethod = updateHandlerType?.GetMethod("ClearCache", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                clearCacheMethod!.Invoke(null, new object?[] { null });
                perGameFactories.Clear();
            };
        }
    }

    static RszGodotConverter()
    {
        ComponentTypes.Init();
    }

    public RszGodotConverter(AssetConfig paths, bool fullImport)
    {
        AssetConfig = paths;
        FullImport = fullImport;
    }

    public void CreateProxyScene(string sourceFilePath, string importFilepath)
    {
        if (!File.Exists(sourceFilePath)) {
            GD.PrintErr("Invalid scene source file, does not exist: " + sourceFilePath);
            return;
        }
        var relativeSourceFile = string.IsNullOrEmpty(AssetConfig.Paths.ChunkPath) ? sourceFilePath : sourceFilePath.Replace(AssetConfig.Paths.ChunkPath, "");

        Directory.CreateDirectory(ProjectSettings.GlobalizePath(importFilepath.GetBaseDir()));

        var name = sourceFilePath.GetFile().GetBaseName().GetBaseName();
        // opening the scene file mostly just to verify that it's valid
        using var scn = OpenScn(sourceFilePath);
        scn.Read();

        if (!ResourceLoader.Exists(importFilepath)) {
            var scene = new PackedScene();
            scene.Pack(new SceneFolder() { Game = AssetConfig.Game, Name = name, Asset = new AssetReference() { AssetFilename = relativeSourceFile } });
            ResourceSaver.Save(scene, importFilepath);
        }
    }

    public void CreateProxyPrefab(string sourceFilePath, string importFilepath)
    {
        if (!File.Exists(sourceFilePath)) {
            GD.PrintErr("Invalid prefab source file, does not exist: " + sourceFilePath);
            return;
        }
        var relativeSourceFile = string.IsNullOrEmpty(AssetConfig.Paths.ChunkPath) ? sourceFilePath : sourceFilePath.Replace(AssetConfig.Paths.ChunkPath, "");

        Directory.CreateDirectory(ProjectSettings.GlobalizePath(importFilepath.GetBaseDir()));

        var name = sourceFilePath.GetFile().GetBaseName().GetBaseName();
        // opening the scene file mostly just to verify that it's valid
        using var file = OpenPfb(sourceFilePath);
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

        ((IRszContainerNode)root).Clear();

        GenerateResources(root, PfbFile.ResourceInfoList, AssetConfig);

        var rootGOs = PfbFile.GameObjectDatas!.OrderBy(o => o.Instance!.Index);
        if (rootGOs.Count() > 1) {
            GD.PrintErr("WTF Capcom, why do you have multiple GameObjects in the PFB root???");
        }
        foreach (var gameObj in rootGOs) {
            Debug.Assert(gameObj.Info != null);
            GenerateGameObject(root, gameObj, root);
        }
    }

    private void GenerateResources(IRszContainerNode root, List<ResourceInfo> resourceInfos, AssetConfig config)
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
                using var childConf = new RszGodotConverter(AssetConfig, FullImport);
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

    private void GenerateGameObject(PrefabNode root, PfbFile.GameObjectData data, REGameObject? parent = null)
    {
        Debug.Assert(data.Info != null);

        var newGameobj = new REGameObject() {
            ObjectId = data.Info.Data.objectId,
            Name = data.Name ?? "UnnamedGameobject",
            Uuid = Guid.NewGuid().ToString(),
            Enabled = true, // TODO which gameobject field is enabled?
            // Enabled = gameObj.Instance.GetFieldValue("v2")
        };
        ((IRszContainerNode)root).AddGameObject(newGameobj, parent);

        foreach (var comp in data.Components.OrderBy(o => o.Index)) {
            SetupComponent(root, comp, newGameobj);
        }

        foreach (var child in data.Children.OrderBy(o => o.Instance!.Index)) {
            GenerateGameObject(root, child, newGameobj);
        }
    }

    private void GenerateGameObject(IRszContainerNode root, ScnFile.GameObjectData data, REGameObject? parent = null)
    {
        Debug.Assert(data.Info != null);

        var newGameobj = new REGameObject() {
            ObjectId = data.Info.Data.objectId,
            Name = data.Name ?? "UnnamedObject",
            Uuid = data.Info.Data.guid.ToString(),
            Prefab = data.Prefab?.Path,
            Enabled = true, // TODO which gameobject field is enabled?
            // Enabled = gameObj.Instance.GetFieldValue("v2")
        };
        root.AddGameObject(newGameobj, parent);

        if (data.Prefab?.Path != null) {
            var importPath = Importer.GetDefaultImportPath(data.Prefab.Path, AssetConfig);
            if (!ResourceLoader.Exists(importPath)) {
                var sourcePath = Importer.ResolveSourceFilePath(data.Prefab.Path, AssetConfig);
                if (!string.IsNullOrEmpty(sourcePath)) {
                    Importer.ImportPrefab(sourcePath, importPath, AssetConfig).Wait();
                }
            }
        }

        foreach (var comp in data.Components.OrderBy(o => o.Index)) {
            SetupComponent(root, comp, newGameobj);
        }

        foreach (var child in data.Children.OrderBy(o => o.Instance!.Index)) {
            GenerateGameObject(root, child, newGameobj);
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

    private void SetupComponent(IRszContainerNode root, RszInstance instance, REGameObject gameObject)
    {
        if (root.Game == SupportedGame.Unknown) {
            GD.PrintErr("Game required on rsz container root for SetupComponent");
            return;
        }

        REComponent? componentInfo;
        if (!perGameFactories.TryGetValue(root.Game, out var factories)) {
            return;
        }
        if (factories.TryGetValue(instance.RszClass.name, out var factory)) {
            componentInfo = factory.Invoke(root, gameObject, instance);
            if (componentInfo == null) {
                componentInfo = new REComponentPlaceholder() { Name = instance.RszClass.name };
                gameObject.AddComponent(componentInfo);
            } else if (gameObject.GetComponent(instance.RszClass.name) == null) {
                gameObject.AddComponent(componentInfo);
            }
        } else {
            componentInfo = new REComponentPlaceholder() { Name = instance.RszClass.name };
            gameObject.AddComponent(componentInfo);
        }

        componentInfo.Classname = instance.RszClass.name;
        componentInfo.ObjectId = instance.Index;
        componentInfo.Data = new REObject(root.Game, instance.RszClass.name, instance);
    }

    public static void DefineComponentFactory(string componentType, Func<IRszContainerNode, REGameObject, RszInstance, REComponent?> factory, params SupportedGame[] supportedGames)
    {
        if (supportedGames.Length == 0) {
            supportedGames = ReachForGodot.GameList;
        }

        foreach (var game in supportedGames) {
            if (!perGameFactories.TryGetValue(game, out var factories)) {
                perGameFactories[game] = factories = new();
            }

            factories[componentType] = factory;
        }
    }

    public void Dispose()
    {
        ScnFile?.Dispose();
        GC.SuppressFinalize(this);
    }
}
