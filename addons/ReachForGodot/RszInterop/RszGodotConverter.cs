namespace RGE;

using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Godot;
using RszTool;
using Shouldly;

public static class PresetImportModeExtensions
{
    public static RszGodotConversionOptions ToOptions(this RszGodotConverter.PresetImportModes mode) => mode switch {
        RszGodotConverter.PresetImportModes.PlaceholderImport => RszGodotConverter.placeholderImport,
        RszGodotConverter.PresetImportModes.ThisFolderOnly => RszGodotConverter.thisFolderOnly,
        RszGodotConverter.PresetImportModes.ImportMissingItems => RszGodotConverter.importMissing,
        RszGodotConverter.PresetImportModes.ImportTreeChanges => RszGodotConverter.importTreeChanges,
        RszGodotConverter.PresetImportModes.ReimportStructure => RszGodotConverter.forceReimportStructure,
        RszGodotConverter.PresetImportModes.FullReimport => RszGodotConverter.fullReimport,
        _ => RszGodotConverter.placeholderImport,
    };
}

public class RszGodotConverter
{
    public static readonly RszGodotConversionOptions placeholderImport = new(RszImportType.Placeholders, RszImportType.Placeholders, RszImportType.Placeholders, RszImportType.Placeholders);
    public static readonly RszGodotConversionOptions thisFolderOnly = new(RszImportType.Placeholders, RszImportType.CreateOrReuse, RszImportType.CreateOrReuse, RszImportType.CreateOrReuse);
    public static readonly RszGodotConversionOptions importMissing = new(RszImportType.CreateOrReuse, RszImportType.CreateOrReuse, RszImportType.CreateOrReuse, RszImportType.CreateOrReuse);
    public static readonly RszGodotConversionOptions importTreeChanges = new(RszImportType.Reimport, RszImportType.Reimport, RszImportType.CreateOrReuse, RszImportType.Reimport);
    public static readonly RszGodotConversionOptions forceReimportStructure = new(RszImportType.ForceReimport, RszImportType.ForceReimport, RszImportType.CreateOrReuse, RszImportType.ForceReimport);
    public static readonly RszGodotConversionOptions fullReimport = new(RszImportType.ForceReimport, RszImportType.ForceReimport, RszImportType.ForceReimport, RszImportType.ForceReimport);

    private static readonly Dictionary<SupportedGame, Dictionary<string, Func<REGameObject, RszInstance, REComponent?>>> perGameFactories = new();

    public enum PresetImportModes
    {
        PlaceholderImport = 0,
        ThisFolderOnly,
        ImportMissingItems,
        ImportTreeChanges,
        ReimportStructure,
        FullReimport
    }

    public AssetConfig AssetConfig { get; }
    public RszGodotConversionOptions Options { get; }

    private RszFileOption fileOption;

    private readonly ImportContext ctx = new();

    private sealed class ImportContext
    {
        public readonly Dictionary<string, Resource?> resolvedResources = new();
        public readonly Dictionary<RszInstance, REObject> importedObjects = new();
        public readonly List<PrefabNode> pendingPrefabs = new();
        public readonly Dictionary<SceneFolder, FolderBatch> sceneBatches = new();

        public readonly Stack<IBatchContext> pendingBatches = new();
        public readonly List<IBatchContext> batches = new();

        public void Clear()
        {
            resolvedResources.Clear();
            importedObjects.Clear();
            pendingPrefabs.Clear();
            pendingBatches.Clear();
        }

        public void QueueBatch(IBatchContext batch)
        {
            batches.Add(batch);
            UpdateUIStatus();
        }

        public void StartBatch(IBatchContext batch)
        {
            pendingBatches.Push(batch);
            UpdateUIStatus();
        }

        public void EndBatch(IBatchContext batch)
        {
            var popped = pendingBatches.Pop();
            Debug.Assert(popped == batch);
            UpdateUIStatus();
        }

        public GameObjectBatch CreatePrefabBatch(PrefabNode root)
        {
            var batch = new GameObjectBatch(this) { GameObject = root };
            QueueBatch(batch);
            return batch;
        }

        public GameObjectBatch CreateGameObjectBatch()
        {
            var batch = new GameObjectBatch(this);
            QueueBatch(batch);
            return batch;
        }

        public FolderBatch CreateFolderBatch(SceneFolder folder, ScnFile.FolderData? data)
        {
            var batch = new FolderBatch(this, folder) { scnData = data };
            QueueBatch(batch);
            return batch;
        }

        private DateTime lastDateLog;

        public void UpdateUIStatus()
        {
            var importer = AsyncImporter.Instance;
            var objs = batches
                .Select(batch => batch.GameObjectCount)
                .Aggregate((total: 0, finished: 0), (sum, batch) => (sum.total + batch.total, sum.finished + batch.finished));

            var comps = batches
                .Select(batch => batch.ComponentsCount)
                .Aggregate((total: 0, finished: 0), (sum, batch) => (sum.total + batch.total, sum.finished + batch.finished));

            var folders = batches
                .Select(batch => batch.FolderCount)
                .Aggregate((total: 0, finished: 0), (sum, batch) => (sum.total + batch.total, sum.finished + batch.finished));

            string? actionLabel = null;
            if (pendingBatches.TryPeek(out var batch)) {
                actionLabel = batch.Label;
            }

            if (importer == null) {
                var now = DateTime.Now;
                if ((now - lastDateLog).Seconds > 1) {
                    GD.Print($"Importer status:\nGame objects: {objs.finished}/{objs.total}\nComponents: {comps.finished}/{comps.total}");
                }
                lastDateLog = now;
            } else {
                importer.SceneCount = folders;
                importer.PrefabCount = objs;
                importer.AssetCount = comps;
                importer.CurrentAction = actionLabel;
            }
        }
    }
    private sealed class GameObjectBatch : IBatchContext
    {
        public REGameObject GameObject = null!;
        public List<Task> ComponentTasks = new();
        public List<GameObjectBatch> Children = new();

        public override string ToString() => GameObject.Owner == null ? GameObject.Name : GameObject.Owner.GetPathTo(GameObject);

        public PrefabQueueParams? prefabData;

        private readonly ImportContext ctx;
        private int compTaskIndex = 0;

        public GameObjectBatch(ImportContext ctx)
        {
            this.ctx = ctx;
        }

        public string Label => "Importing prefab...";
        public bool IsFinished => compTaskIndex >= ComponentTasks.Count && Children.All(c => c.IsFinished);

        public (int total, int finished) FolderCount => (0, 0);

        public (int total, int finished) ComponentsCount => (ComponentTasks.Count, compTaskIndex);
        public (int total, int finished) GameObjectCount => (1, IsFinished ? 1 : 0);

        public async Task Await(RszGodotConverter converter)
        {
            ctx.StartBatch(this);
            if (prefabData != null) {
                await converter.RePrepareBatchedPrefabGameObject(this);
            }
            ctx.UpdateUIStatus();

            while (compTaskIndex < ComponentTasks.Count) {
                await ComponentTasks[compTaskIndex];
                compTaskIndex++;
                ctx.UpdateUIStatus();
            }

            foreach (var ch in Children) {
                await ch.Await(converter);
                ctx.UpdateUIStatus();
            }
            Children.Clear();
            ctx.EndBatch(this);
        }
    }

    private sealed record PrefabQueueParams(PackedScene prefab, IGameObjectData data, RszImportType importType, Node? parentNode, REGameObject? parent = null, int dedupeIndex = 0);

    private sealed class FolderBatch : IBatchContext
    {
        public override string ToString() => folder.Name;

        public readonly List<FolderBatch> folders = new();
        public readonly List<GameObjectBatch> gameObjects = new List<GameObjectBatch>();
        public readonly HashSet<SceneFolderProxy> finishedFolders = new();
        public ScnFile.FolderData? scnData;
        public readonly SceneFolder folder;
        private readonly ImportContext ctx;

        public int FinishedFolderCount { get; internal set; }

        public FolderBatch(ImportContext importContext, SceneFolder folder)
        {
            this.ctx = importContext;
            this.folder = folder;
        }

        public string Label => "Importing folder...";
        public bool IsFinished => gameObjects.Count == 0 && folders.Count == 0;

        public int TotalCount => throw new NotImplementedException();

        public (int total, int finished) FolderCount => (folders.Count(f => f.folder.ParentFolder == folder), FinishedFolderCount);
        public (int total, int finished) ComponentsCount => (0, 0);
        public (int total, int finished) GameObjectCount => (0, 0);

        public async Task AwaitGameObjects(RszGodotConverter converter)
        {
            foreach (var subtask in gameObjects) {
                await subtask.Await(converter);
            }
            // await Task.WhenAll(gameObjects.Select(c => c.Await(converter)));
        }
    }

    private sealed record BatchInfo(string label, IBatchContext status);
    private interface IBatchContext
    {
        string Label { get; }
        bool IsFinished { get; }
        public (int total, int finished) FolderCount { get; }
        public (int total, int finished) ComponentsCount { get; }
        public (int total, int finished) GameObjectCount { get; }
    }


    static RszGodotConverter()
    {
        AssemblyLoadContext.GetLoadContext(typeof(RszGodotConverter).Assembly)!.Unloading += (c) => {
            perGameFactories.Clear();
        };
        InitComponents(typeof(RszGodotConverter).Assembly);
    }

    public static void InitComponents(Assembly assembly)
    {
        var componentTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<REComponentClassAttribute>() != null && !t.IsAbstract);

        foreach (var type in componentTypes) {
            if (!type.IsAssignableTo(typeof(REComponent))) {
                GD.PrintErr($"Invalid REComponentClass annotated type {type.FullName}.\nMust be a non-abstract REComponent node.");
                continue;
            }
            if (type.GetCustomAttribute<ToolAttribute>() == null || type.GetCustomAttribute<GlobalClassAttribute>() == null) {
                GD.PrintErr($"REComponentClass annotated type {type.FullName} must also be [Tool] and [GlobalClass].");
                continue;
            }

            var attr = type.GetCustomAttribute<REComponentClassAttribute>()!;
            DefineComponentFactory(attr.Classname, (obj, instance) => {
                var node = (REComponent)Activator.CreateInstance(type)!;
                return node;
            }, attr.SupportedGames);
        }
    }

    public static void DefineComponentFactory(string componentType, Func<REGameObject, RszInstance, REComponent?> factory, params SupportedGame[] supportedGames)
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

    public RszGodotConverter(AssetConfig paths, RszGodotConversionOptions options)
    {
        AssetConfig = paths;
        Options = options;
        fileOption = TypeCache.CreateRszFileOptions(AssetConfig);
    }

    public PackedScene? CreateOrReplaceScene(string sourceFilePath, string importFilepath)
    {
        return SaveOrReplaceSceneResource<SceneFolder>(sourceFilePath, importFilepath);
    }

    public PackedScene? CreateOrReplacePrefab(string sourceFilePath, string importFilepath)
    {
        return SaveOrReplaceSceneResource<PrefabNode>(sourceFilePath, importFilepath);
    }

    public UserdataResource? CreateOrReplaceUserdata(string sourceFilePath, string importFilepath)
    {
        UserdataResource userdata = new UserdataResource() { ResourceType = RESupportedFileFormats.Userdata };
        return SaveOrReplaceRszResource(userdata, sourceFilePath, importFilepath);
    }

    private PackedScene? SaveOrReplaceSceneResource<TRoot>(string sourceFilePath, string importFilepath) where TRoot : Node, IRszContainerNode, new()
    {
        var relativeSourceFile = AssetConfig.Paths.GetChunkRelativePath(sourceFilePath);
        var name = sourceFilePath.GetFile().GetBaseName().GetBaseName();
        var scene = new PackedScene();
        scene.Pack(new TRoot() { Game = AssetConfig.Game, Name = name, Asset = new AssetReference(relativeSourceFile) });
        return SaveOrReplaceResource(scene, sourceFilePath, importFilepath);
    }

    private PackedScene? UpdateSceneResource<TRoot>(TRoot root) where TRoot : Node, IRszContainerNode, new()
    {
        var relativeSourceFile = root.Asset!.AssetFilename;
        var sourceFilePath = PathUtils.ResolveSourceFilePath(relativeSourceFile, AssetConfig) ?? throw new Exception("Invalid source asset file " + relativeSourceFile);
        var importFilepath = PathUtils.GetLocalizedImportPath(relativeSourceFile, AssetConfig) ?? throw new Exception("Couldn't resolve import path for resource " + relativeSourceFile);
        var name = sourceFilePath.GetFile().GetBaseName().GetBaseName();
        var scene = ResourceLoader.Exists(importFilepath) ? ResourceLoader.Load<PackedScene>(importFilepath) : new PackedScene();
        scene.Pack(root);
        return SaveOrReplaceResource(scene, sourceFilePath, importFilepath);
    }

    private TRes? SaveOrReplaceRszResource<TRes>(TRes newResource, string sourceFilePath, string importFilepath) where TRes : Resource, IRszContainerNode
    {
        if (!File.Exists(sourceFilePath)) {
            GD.PrintErr("Invalid resource source file, does not exist: " + sourceFilePath);
            return null;
        }

        var relativeSourceFile = AssetConfig.Paths.GetChunkRelativePath(sourceFilePath);
        var name = sourceFilePath.GetFile().GetBaseName().GetBaseName();

        newResource.ResourceName = name;
        newResource.ResourcePath = importFilepath;
        newResource.Game = AssetConfig.Game;
        newResource.Asset = new AssetReference(relativeSourceFile);

        return SaveOrReplaceResource(newResource, sourceFilePath, importFilepath);
    }

    private TRes? SaveOrReplaceResource<TRes>(TRes newResource, string sourceFilePath, string importFilepath) where TRes : Resource
    {
        if (!File.Exists(sourceFilePath)) {
            GD.PrintErr("Invalid resource source file, does not exist: " + sourceFilePath);
            ctx.resolvedResources[sourceFilePath] = null;
            return null;
        }

        if (ResourceLoader.Exists(importFilepath)) {
            newResource.TakeOverPath(importFilepath);
        } else {
            Directory.CreateDirectory(ProjectSettings.GlobalizePath(importFilepath.GetBaseDir()));
            newResource.ResourcePath = importFilepath;
        }
        GD.Print(" Saving resource " + importFilepath);
        ResourceSaver.Save(newResource);
        ctx.resolvedResources[importFilepath] = newResource;
        Importer.QueueFileRescan();
        return newResource;
    }

    public async Task RegenerateSceneTree(SceneFolder root)
    {
        ctx.Clear();
        await GenerateSceneTree(root);
        ctx.Clear();
    }

    public async Task RegeneratePrefabTree(PrefabNode root)
    {
        ctx.Clear();
        await GeneratePrefabTree(root);
        ctx.Clear();
    }

    private async Task GenerateSceneTree(SceneFolder root)
    {
        var scnFullPath = root.Asset?.ResolveSourceFile(AssetConfig);
        if (scnFullPath == null) return;

        GD.Print("Opening scn file " + scnFullPath);
        using var file = new ScnFile(fileOption, new FileHandler(scnFullPath));
        try {
            file.Read();
        } catch (RszRetryOpenException e) {
            e.LogRszRetryException();
            await GenerateSceneTree(root);
            return;
        } catch (Exception e) {
            GD.PrintErr("Failed to parse file " + scnFullPath, e);
            return;
        }

        file.SetupGameObjects();

        if (Options.folders == RszImportType.ForceReimport) {
            root.Clear();
        }

        var batch = ctx.CreateFolderBatch(root, null);
        ctx.StartBatch(batch);
        GenerateResources(root, file.ResourceInfoList, AssetConfig);
        PrepareFolderBatch(batch, file.GameObjectDatas!.Where(go => go.Info?.Data.parentId == -1), file.FolderDatas!);
        await AwaitFolderBatch(batch);
        ctx.UpdateUIStatus();

        root.RecalculateBounds(true);

        ctx.EndBatch(batch);

        GD.Print(" Finished scene tree " + root.Name);
        if (!root.IsInsideTree()) {
            UpdateSceneResource(root);
        }
    }

    private void PrepareFolderBatch(FolderBatch batch, IEnumerable<ScnFile.GameObjectData> gameobjects, IEnumerable<ScnFile.FolderData> folders)
    {
        var dupeDict = new Dictionary<string, int>();
        foreach (var gameObj in gameobjects) {
            Debug.Assert(gameObj.Info != null);

            var objBatch = ctx.CreateGameObjectBatch();
            batch.gameObjects.Add(objBatch);

            var childName = gameObj.Name ?? "unnamed";
            if (dupeDict.TryGetValue(childName, out var index)) {
                dupeDict[childName] = ++index;
            } else {
                dupeDict[childName] = index = 0;
            }
            PrepareGameObjectBatch(gameObj, Options.folders, objBatch, batch.folder, null, index);
        }

        foreach (var folder in folders) {
            Debug.Assert(folder.Info != null);

            PrepareSubfolderPlaceholders(batch.folder, folder, batch.folder, batch);
        }
    }

    private async Task AwaitFolderBatch(FolderBatch batch)
    {
        ctx.StartBatch(batch);

        var folder = batch.folder;
        if (folder is SceneFolderProxy proxy) {
            var tempInstance = proxy.Contents!.Instantiate<SceneFolder>();
            if (!batch.finishedFolders.Contains(proxy)) {
                await GenerateSceneTree(tempInstance);
                ctx.UpdateUIStatus();
                proxy.KnownBounds = tempInstance.KnownBounds;
                proxy.Contents.Pack(tempInstance);
                var importPath = proxy.Asset?.GetImportFilepath(AssetConfig);
                var childFullPath = proxy.Asset?.ResolveSourceFile(AssetConfig);
                batch.finishedFolders.Add(proxy);

                if (importPath == null || childFullPath == null) {
                    GD.PrintErr("Invalid folder source file " + proxy.Asset?.ToString());
                    return;
                }
            }
            proxy.Enabled = true; // TODO configurable by import type?
        }

        await batch.AwaitGameObjects(this);
        for (var i = 0; i < batch.folders.Count; i++) {
            FolderBatch subfolder = batch.folders[i];
            await AwaitFolderBatch(subfolder);
            batch.FinishedFolderCount++;
        }

        ctx.EndBatch(batch);
    }

    private async Task GenerateLinkedPrefabTree(PrefabNode root)
    {
        await GeneratePrefabTree(root);
        UpdateSceneResource(root);
    }

    private async Task GeneratePrefabTree(PrefabNode root)
    {
        var scnFullPath = PathUtils.ResolveSourceFilePath(root.Asset!.AssetFilename, AssetConfig);
        if (scnFullPath == null) return;

        GD.Print("Opening pfb file " + scnFullPath);
        using var file = new PfbFile(fileOption, new FileHandler(scnFullPath));
        try {
            file.Read();
            file.SetupGameObjects();
        } catch (Exception e) {
            GD.PrintErr("Failed to parse file " + scnFullPath, e);
            return;
        }

        GenerateResources(root, file.ResourceInfoList, AssetConfig);
        var batch = ctx.CreatePrefabBatch(root);

        if (Options.prefabs == RszImportType.ForceReimport) {
            root.Clear();
        }

        var rootGOs = file.GameObjectDatas!.OrderBy(o => o.Instance!.Index);
        Debug.Assert(rootGOs.Count() <= 1, "WTF Capcom?? Guess we doing multiple PFB roots now");
        foreach (var gameObj in rootGOs) {
            root.OriginalName = gameObj.Name ?? root.Name;
            PrepareGameObjectBatch(gameObj, Options.prefabs, batch, root);
            await batch.Await(this);
        }

        foreach (var go in root.AllChildrenIncludingSelf) {
            foreach (var comp in go.Components) {
                ReconstructGameObjectRefs(file, comp, comp, go, root);
            }
        }
    }

    private NodePath? ResolveGameObjectRef(PfbFile file, REField field, REObject obj, ref RszInstance? instance, REComponent component, REGameObject root, int arrayIndex)
    {
        // god help me...
        instance ??= ctx.importedObjects.FirstOrDefault(kv => kv.Value == obj).Key;
        Debug.Assert(instance != null);
        int idx = instance.ObjectTableIndex;

        var fieldRefs = file.GameObjectRefInfoList.Where(rr => rr.Data.objectId == idx && rr.Data.arrayIndex == arrayIndex);
        if (!fieldRefs.Any()) return null;

        var cache = TypeCache.GetData(AssetConfig.Game, obj.Classname!);
        var propInfoDict = cache.PfbRefs;
        if (!propInfoDict.TryGetValue(field.SerializedName, out var propInfo)) {
            var allFieldRefs = file.GameObjectRefInfoList.Where(rr => rr.Data.objectId == idx && rr.Data.arrayIndex == 0);
            var refcount = allFieldRefs.Count();
            Debug.Assert(refcount > 0);
            if (refcount == 1) {
                var propId = allFieldRefs.First().Data.propertyId;
                propInfo = new PrefabGameObjectRefProperty() { PropertyId = propId, AutoDetected = true };
                propInfoDict[field.SerializedName] = propInfo;
                GD.PrintErr("Auto-detected GameObjectRef property " + field.SerializedName + " as propertyId " + propId + ". May be wrong?");
                TypeCache.UpdateTypecacheEntry(AssetConfig.Game, obj.Classname!, propInfoDict);
            } else {
                GD.PrintErr("Found undeclared GameObjectRef property " + field.SerializedName + " in class " + obj.Classname);
                return default;
            }
        }

        var objref = fieldRefs.FirstOrDefault(rr => rr.Data.propertyId == propInfo.PropertyId);
        if (objref == null) {
            GD.PrintErr("Could not match GameObjectRef field ref");
            return default;
        }

        var targetInstance = file.RSZ?.ObjectList[(int)objref.Data.targetId];
        if (targetInstance == null) {
            GD.PrintErr("GameObjectRef target object not found");
            return default;
        }

        if (!ctx.importedObjects.TryGetValue(targetInstance, out var targetGameobjData)) {
            GD.Print("Referenced game object was not imported");
            return default;
        }

        var targetGameobj = root.AllChildrenIncludingSelf.FirstOrDefault(x => x.Data == targetGameobjData);
        if (targetGameobj == null) {
            GD.Print("Could not find actual gameobject instance");
            return default;
        }

        return root.GetPathTo(targetGameobj);
    }

    private void ReconstructGameObjectRefs(PfbFile file, REObject obj, REComponent component, REGameObject gameobj, REGameObject root, int arrayIndex = 0)
    {
        RszInstance? instance = null;
        foreach (var field in obj.TypeInfo.Fields) {
            if (field.RszField.type == RszFieldType.GameObjectRef) {
                if (field.RszField.array) {
                    instance ??= ctx.importedObjects.FirstOrDefault(kv => kv.Value == obj).Key;
                    var indices = (IList<object>)instance.Values[field.FieldIndex];
                    var paths = new Godot.Collections.Array<NodePath>();
                    for (int i = 0; i < indices.Count; ++i) {
                        var refval = ResolveGameObjectRef(file, field, obj, ref instance, component, root, i);
                        paths.Add(refval ?? new NodePath(""));
                    }
                    obj.SetField(field, paths);
                } else {
                    var refval = ResolveGameObjectRef(file, field, obj, ref instance, component, root, 0);
                    obj.SetField(field, refval ?? new Variant());
                }

                // GD.Print("Found GameObjectRef link: " + obj + " => " + targetGameobj + " == " + obj.GetField(field));
            } else if (field.RszField.type is RszFieldType.Object) {
                if (!obj.TryGetFieldValue(field, out var child)) continue;

                if (field.RszField.array) {
                    if (child.AsGodotArray<REObject>() is Godot.Collections.Array<REObject> children) {
                        int i = 0;
                        foreach (var childObj in children) {
                            if (childObj != null) {
                                ReconstructGameObjectRefs(file, childObj, component, gameobj, root, i++);
                            }
                        }
                    }
                } else {
                    if (child.As<REObject>() is REObject childObj) {
                        ReconstructGameObjectRefs(file, childObj, component, gameobj, root);
                    }
                }
            }
        }
    }

    public void GenerateUserdata(UserdataResource root)
    {
        var scnFullPath = PathUtils.ResolveSourceFilePath(root.Asset!.AssetFilename, AssetConfig);
        if (scnFullPath == null) return;

        GD.Print("Opening user file " + scnFullPath);
        using var file = new UserFile(fileOption, new FileHandler(scnFullPath));
        try {
            file.Read();
        } catch (Exception e) {
            GD.PrintErr("Failed to parse file " + scnFullPath, e);
            return;
        }

        root.Clear();

        GenerateResources(root, file.ResourceInfoList, AssetConfig);

        if (file.RSZ!.ObjectList.Skip(1).Any()) {
            GD.PrintErr("WTF Capcom, why do you have multiple objects in the userfile root???");
        }

        foreach (var instance in file.RSZ!.ObjectList) {
            root.Classname = instance.RszClass.name;
            ApplyObjectValues(root, instance);
            ResourceSaver.Save(root);
            break;
        }
    }

    private void GenerateResources(IRszContainerNode root, List<ResourceInfo> resourceInfos, AssetConfig config)
    {
        var resources = new List<REResource>(resourceInfos.Count);
        foreach (var res in resourceInfos) {
            if (!string.IsNullOrWhiteSpace(res.Path)) {
                var resource = Importer.FindOrImportResource<Resource>(res.Path, config);
                if (resource == null) {
                    resources.Add(new REResource() {
                        Asset = new AssetReference(res.Path),
                        ResourceType = PathUtils.GetFileFormat(res.Path).format,
                        Game = AssetConfig.Game,
                        ResourceName = res.Path.GetFile()
                    });
                } else if (resource is REResource reres) {
                    resources.Add(reres);
                } else {
                    resources.Add(new REResourceProxy() {
                        Asset = new AssetReference(res.Path),
                        ResourceType = PathUtils.GetFileFormat(res.Path).format,
                        ImportedResource = resource,
                        Game = AssetConfig.Game,
                        ResourceName = res.Path.GetFile()
                    });
                }
            } else {
                GD.Print("Found a resource with null path: " + resources.Count);
            }
        }
        root.Resources = resources.ToArray();
    }

    private void PrepareSubfolderPlaceholders(SceneFolder root, ScnFile.FolderData folder, SceneFolder parent, FolderBatch batch)
    {
        Debug.Assert(folder.Info != null);
        var name = folder.Name ?? "UnnamedFolder";
        var subfolder = parent.GetFolder(name);
        if (folder.Instance?.GetFieldValue("v5") is string scnPath && !string.IsNullOrWhiteSpace(scnPath)) {
            var isNew = false;
            if (subfolder == null) {
                subfolder = new SceneFolderProxy() { Name = name, Asset = new AssetReference(scnPath), Game = AssetConfig.Game };
                parent.AddFolder(subfolder);
                isNew = true;
            }
            var subProxy = (SceneFolderProxy)subfolder;
            subProxy.Contents = Importer.FindOrImportResource<PackedScene>(subfolder.Asset!.AssetFilename, AssetConfig);

            if (subfolder.Name != name) {
                GD.PrintErr("Parent and child scene name mismatch? " + subfolder.Name + " => " + name);
            }
            subfolder.Name = name;
            if (folder.Children.Any()) {
                GD.PrintErr($"Unexpected situation: resource-linked scene also has additional children in parent scene.\nParent scene:{root.Asset?.AssetFilename}\nSubfolder:{scnPath}");
            }

            if (Options.folders == RszImportType.Placeholders || !isNew && Options.folders == RszImportType.CreateOrReuse) {
                return;
            }

            subProxy.UnloadScene();
            var newBatch = ctx.CreateFolderBatch(subfolder, folder);
            batch.folders.Add(newBatch);
        } else {
            if (subfolder == null) {
                GD.Print("Creating folder " + name);
                subfolder = new SceneFolder() {
                    Game = root.Game,
                    Name = name
                };
                parent.AddFolder(subfolder);
            } else {
                GD.Print("Found existing folder " + name);
                if (!string.IsNullOrEmpty(subfolder.SceneFilePath)) {
                    GD.PrintErr($"Found local folder that was also instantiated from a scene - could this be problematic?\nParent scene:{root.Asset?.AssetFilename}\nSubfolder:{name}");
                }
            }

            var newBatch = ctx.CreateFolderBatch(subfolder, folder);
            batch.folders.Add(newBatch);
            PrepareFolderBatch(newBatch, folder.GameObjects, folder.Children);
        }
    }

    private void GenerateSceneGameObject(SceneFolder currentFolder, ScnFile.GameObjectData data, FolderBatch folderBatch)
    {
        var batch = ctx.CreateGameObjectBatch();
        folderBatch.gameObjects.Add(batch);
        PrepareGameObjectBatch(data, Options.folders, batch, currentFolder, null, 0);
    }

    private async Task RePrepareBatchedPrefabGameObject(GameObjectBatch batch)
    {
        Debug.Assert(batch.prefabData != null);
        var (prefab, data, importType, parentNode, parentGO, index) = batch.prefabData;
        var pfbInstance = prefab.Instantiate<PrefabNode>(PackedScene.GenEditState.Instance);
        await GenerateLinkedPrefabTree(pfbInstance);
        PrepareGameObjectBatch(data, importType, batch, parentNode, parentGO, index);
    }

    private void PrepareGameObjectBatch(IGameObjectData data, RszImportType importType, GameObjectBatch batch, Node? parentNode, REGameObject? parent = null, int dedupeIndex = 0)
    {
        var name = data.Name ?? "UnnamedGameObject";

        Debug.Assert(data.Instance != null);

        string? uuid = null;
        REGameObject? gameobj = batch.GameObject;
        if (gameobj == null && parentNode != null) {
            if (parentNode is REGameObject obj) {
                gameobj = obj.GetChild(name, dedupeIndex);
            } else if (parentNode is SceneFolder scn) {
                gameobj = scn.GetGameObject(name, dedupeIndex);
            } else {
                Debug.Assert(false);
            }
            batch.GameObject = gameobj!;
        }

        if (data is ScnFile.GameObjectData scnData) {
            uuid = scnData.Info?.Data.guid.ToString();

            // note: some PFB files aren't shipped with the game, hence the CheckResourceExists check
            // presumably they are only used directly within scn files and not instantiated during runtime
            if (!string.IsNullOrEmpty(scnData.Prefab?.Path) && Importer.CheckResourceExists(scnData.Prefab.Path, AssetConfig)) {
                if (ctx.resolvedResources.TryGetValue(PathUtils.GetLocalizedImportPath(scnData.Prefab.Path, AssetConfig)!, out var pfb) && pfb is PackedScene resolvedPfb) {
                    gameobj = resolvedPfb.Instantiate<PrefabNode>(PackedScene.GenEditState.Instance);
                } else if (Importer.FindOrImportResource<PackedScene>(scnData.Prefab.Path, AssetConfig) is PackedScene packedPfb) {
                    var pfbInstance = packedPfb.Instantiate<PrefabNode>(PackedScene.GenEditState.Instance);
                    gameobj = pfbInstance;

                    if (importType == RszImportType.Placeholders) {
                        gameobj.Name = name;
                        if (data.Components.FirstOrDefault(t => t.RszClass.name == "via.Transform") is RszInstance transform) {
                            RETransformComponent.ApplyTransform(gameobj, transform);
                        }
                        return;
                    }
                    batch.prefabData = new PrefabQueueParams(packedPfb, data, importType, parentNode, parent, dedupeIndex);
                    return;
                } else {
                    GD.Print("Prefab source file not found: " + scnData.Prefab.Path);
                }
            }
        }

        if (gameobj == null) {
            gameobj ??= new REGameObject() {
                Game = AssetConfig.Game,
                Name = name,
                OriginalName = name,
                Enabled = true, // TODO which gameobject field is enabled?
                // Enabled = gameObj.Instance.GetFieldValue("v2")
            };
        }
        batch.GameObject = gameobj;

        if (uuid != null) {
            gameobj.Uuid = uuid;
        }

        gameobj.Data = CreateOrUpdateObject(data.Instance, gameobj.Data);

        if (gameobj.GetParent() == null && parentNode != null && parentNode != gameobj) {
            parentNode.AddUniqueNamedChild(gameobj);
            var owner = parentNode?.Owner ?? parentNode;
            Debug.Assert(owner != gameobj);
            gameobj.Owner = owner;
        }

        if (importType == RszImportType.Placeholders) {
            gameobj.Name = name;
            gameobj.Components = new();
            if (data.Components.FirstOrDefault(t => t.RszClass.name == "via.Transform") is RszInstance transform) {
                RETransformComponent.ApplyTransform(gameobj, transform);
            }
        } else {
            foreach (var comp in data.Components.OrderBy(o => o.Index)) {
                SetupComponent(comp, batch, importType);
            }
        }

        var dupeDict = new Dictionary<string, int>();
        foreach (var child in data.GetChildren().OrderBy(o => o.Instance!.Index)) {
            var childName = child.Name ?? "unnamed";
            if (dupeDict.TryGetValue(childName, out var index)) {
                dupeDict[childName] = ++index;
            } else {
                dupeDict[childName] = index = 0;
            }
            var childBatch = ctx.CreateGameObjectBatch();
            batch.Children.Add(childBatch);
            PrepareGameObjectBatch(child, importType, childBatch, gameobj, gameobj, index);
        }
    }

    private void SetupComponent(RszInstance instance, GameObjectBatch batch, RszImportType importType)
    {
        if (AssetConfig.Game == SupportedGame.Unknown) {
            GD.PrintErr("Game required on rsz container root for SetupComponent");
            return;
        }
        var gameObject = batch.GameObject;

        var classname = instance.RszClass.name;
        var componentInfo = gameObject.GetComponent(classname);
        if (componentInfo != null) {
            // nothing to do here
        } else if (
            perGameFactories.TryGetValue(AssetConfig.Game, out var factories) &&
            factories.TryGetValue(classname, out var factory)
        ) {
            componentInfo = factory.Invoke(gameObject, instance);
            if (componentInfo == null) {
                componentInfo = new REComponentPlaceholder();
                gameObject.AddComponent(componentInfo);
            } else if (gameObject.GetComponent(classname) == null) {
                // if the component was created but not actually added to the gameobject yet, do so now
                gameObject.AddComponent(componentInfo);
            }
        } else {
            componentInfo = new REComponentPlaceholder();
            gameObject.AddComponent(componentInfo);
        }

        componentInfo.Classname = classname;
        componentInfo.Game = AssetConfig.Game;
        componentInfo.ResourceName = classname;
        componentInfo.GameObject = gameObject;
        ApplyObjectValues(componentInfo, instance);
        var setupTask = componentInfo.Setup(gameObject, instance, Options.meshes);
        if (!setupTask.IsCompleted) {
            batch.ComponentTasks.Add(setupTask);
        }
    }

    private REObject CreateOrUpdateObject(RszInstance instance, REObject? obj)
    {
        return obj == null ? CreateOrGetObject(instance) : ApplyObjectValues(obj, instance);
    }

    private REObject CreateOrGetObject(RszInstance instance)
    {
        if (ctx.importedObjects.TryGetValue(instance, out var obj)) {
            return obj;
        }

        obj = new REObject(AssetConfig.Game, instance.RszClass.name);
        ctx.importedObjects[instance] = obj;
        return ApplyObjectValues(obj, instance);
    }

    private REObject ApplyObjectValues(REObject obj, RszInstance instance)
    {
        ctx.importedObjects[instance] = obj;
        foreach (var field in obj.TypeInfo.Fields) {
            var value = instance.Values[field.FieldIndex];
            obj.SetField(field, ConvertRszValue(field, value));
        }

        return obj;
    }

    private Variant ConvertRszValue(REField field, object value)
    {
        if (field.RszField.array) {
            if (value == null) return new Godot.Collections.Array();

            var type = value.GetType();
            object[] arr = ((IList<object>)value).ToArray();
            var newArray = new Godot.Collections.Array();
            foreach (var v in arr) {
                newArray.Add(ConvertSingleRszValue(field, v));
            }
            return newArray;
        }

        return ConvertSingleRszValue(field, value);
    }

    private Variant ConvertSingleRszValue(REField field, object value)
    {
        switch (field.RszField.type) {
            case RszFieldType.UserData:
                return ConvertUserdata((RszInstance)value);
            case RszFieldType.Object:
                var fieldInst = (RszInstance)value;
                return fieldInst.Index == 0 ? new Variant() : CreateOrGetObject(fieldInst);
            case RszFieldType.Resource:
                if (value is string str && !string.IsNullOrWhiteSpace(str)) {
                    return Importer.FindOrImportResource<Resource>(str, ReachForGodot.GetAssetConfig(AssetConfig.Game))!;
                } else {
                    return new Variant();
                }
            default:
                return RszTypeConverter.FromRszValueSingleValue(field, value, AssetConfig.Game);
        }
    }

    private Variant ConvertRszInstanceArray(object value)
    {
        var values = (IList<object>)value;
        var newArray = new Godot.Collections.Array();
        foreach (var element in values) {
            if (element is RszInstance rsz) {
                newArray.Add(rsz.Index == 0 ? new Variant() : CreateOrGetObject(rsz));
            } else {
                GD.PrintErr("INVALID ARRAY WTF");
            }
        }
        return newArray;
    }

    private Variant ConvertUserdata(RszInstance rsz)
    {
        if (ctx.importedObjects.TryGetValue(rsz, out var previousInst)) {
            return previousInst;
        }
        if (rsz.Index == 0) return new Variant();

        if (rsz.RSZUserData is RSZUserDataInfo ud1) {
            if (!string.IsNullOrEmpty(ud1.Path)) {
                return ctx.importedObjects[rsz] = Importer.FindOrImportResource<UserdataResource>(ud1.Path, AssetConfig)!;
            }
        } else if (rsz.RSZUserData is RSZUserDataInfo_TDB_LE_67 ud2) {
            GD.PrintErr("Unsupported userdata reference TDB_LE_67");
        } else if (string.IsNullOrEmpty(rsz.RszClass.name)) {
            return new Variant();
        } else {
            GD.PrintErr("Unhandled userdata reference type??");
        }
        return new Variant();
    }
}

public record RszGodotConversionOptions(
    RszImportType folders = RszImportType.Placeholders,
    RszImportType prefabs = RszImportType.CreateOrReuse,
    RszImportType meshes = RszImportType.CreateOrReuse,
    RszImportType userdata = RszImportType.Placeholders
);

public enum RszImportType
{
    /// <summary>If an asset does not exist, only create a placeholder resource for it.</summary>
    Placeholders,
    /// <summary>If an asset does not exist or is merely a placeholder, import and generate its data. Do nothing if any of its contents are already imported.</summary>
    CreateOrReuse,
    /// <summary>Reimport the full asset from the source file, maintaining any local changes as much as possible.</summary>
    Reimport,
    /// <summary>Discard any local data and regenerate assets.</summary>
    ForceReimport,
}
