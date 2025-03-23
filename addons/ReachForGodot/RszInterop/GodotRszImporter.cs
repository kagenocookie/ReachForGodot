namespace ReaGE;

using System;
using System.Threading.Tasks;
using Godot;
using RszTool;

public static class PresetImportModeExtensions
{
    public static RszGodotConversionOptions ToOptions(this GodotRszImporter.PresetImportModes mode) => mode switch {
        GodotRszImporter.PresetImportModes.PlaceholderImport => GodotRszImporter.placeholderImport,
        GodotRszImporter.PresetImportModes.ThisFolderOnly => GodotRszImporter.thisFolderOnly,
        GodotRszImporter.PresetImportModes.ImportMissingItems => GodotRszImporter.importMissing,
        GodotRszImporter.PresetImportModes.ImportTreeChanges => GodotRszImporter.importTreeChanges,
        GodotRszImporter.PresetImportModes.ReimportStructure => GodotRszImporter.forceReimportStructure,
        GodotRszImporter.PresetImportModes.FullReimport => GodotRszImporter.fullReimport,
        _ => GodotRszImporter.placeholderImport,
    };
}

public class GodotRszImporter
{
    public static readonly RszGodotConversionOptions placeholderImport = new(RszImportType.Placeholders, RszImportType.Placeholders, RszImportType.Placeholders, RszImportType.Placeholders);
    public static readonly RszGodotConversionOptions thisFolderOnly = new(RszImportType.Placeholders, RszImportType.Reimport, RszImportType.CreateOrReuse, RszImportType.Reimport);
    public static readonly RszGodotConversionOptions importMissing = new(RszImportType.CreateOrReuse, RszImportType.CreateOrReuse, RszImportType.CreateOrReuse, RszImportType.CreateOrReuse);
    public static readonly RszGodotConversionOptions importTreeChanges = new(RszImportType.Reimport, RszImportType.Reimport, RszImportType.CreateOrReuse, RszImportType.Reimport);
    public static readonly RszGodotConversionOptions forceReimportStructure = new(RszImportType.ForceReimport, RszImportType.ForceReimport, RszImportType.CreateOrReuse, RszImportType.ForceReimport);
    public static readonly RszGodotConversionOptions fullReimport = new(RszImportType.ForceReimport, RszImportType.ForceReimport, RszImportType.ForceReimport, RszImportType.ForceReimport);

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
        public readonly Dictionary<REObject, RszInstance> objectSourceInstances = new();
        public readonly Dictionary<Guid, GameObject> gameObjects = new();
        public readonly List<PrefabNode> pendingPrefabs = new();
        public readonly Dictionary<SceneFolder, FolderBatch> sceneBatches = new();

        public readonly Stack<IBatchContext> pendingBatches = new();
        public readonly List<IBatchContext> batches = new();

        private DateTime lastDateLog;
        private DateTime lastStatusUpdateTime;

        public bool IsCancelled { get; internal set; }

        public void Clear()
        {
            resolvedResources.Clear();
            importedObjects.Clear();
            objectSourceInstances.Clear();
            pendingPrefabs.Clear();
            gameObjects.Clear();
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

        public GameObjectBatch CreatePrefabBatch(PrefabNode root, string? note)
        {
            var batch = new GameObjectBatch(this, note) { GameObject = root };
            QueueBatch(batch);
            return batch;
        }

        public GameObjectBatch CreateGameObjectBatch(string? note)
        {
            var batch = new GameObjectBatch(this, note);
            QueueBatch(batch);
            return batch;
        }

        public FolderBatch CreateFolderBatch(SceneFolder folder, ScnFile.FolderData? data, string? note)
        {
            var batch = new FolderBatch(this, folder, note) { scnData = data };
            QueueBatch(batch);
            return batch;
        }

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

        public async Task MaybeYield()
        {
            if (IsCancelled) {
                throw new TaskCanceledException("Asset import has been cancelled.");
            }

            var now = DateTime.Now;
            if ((now - lastStatusUpdateTime).Seconds > 1) {
                UpdateUIStatus();
                await Task.Delay(25);

                lastStatusUpdateTime = now;
            }
        }
    }

    private sealed class GameObjectBatch : IBatchContext
    {
        public GameObject GameObject = null!;
        public List<Task> ComponentTasks = new();
        public List<GameObjectBatch> Children = new();

        public override string ToString() => GameObject.Owner == null ? GameObject.Name : GameObject.Owner.GetPathTo(GameObject);

        public PrefabQueueParams? prefabData;

        private readonly ImportContext ctx;
        private readonly string? note;
        private int compTaskIndex = 0;

        public GameObjectBatch(ImportContext ctx, string? note)
        {
            this.ctx = ctx;
            this.note = note;
        }

        public string Label => $"Importing GameObject {note}...";
        public bool IsFinished => compTaskIndex >= ComponentTasks.Count && Children.All(c => c.IsFinished);

        public (int total, int finished) FolderCount => (0, 0);
        public (int total, int finished) ComponentsCount => (ComponentTasks.Count, compTaskIndex);
        public (int total, int finished) GameObjectCount => (1, IsFinished ? 1 : 0);

        public async Task Await(GodotRszImporter converter)
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

    private sealed record PrefabQueueParams(PackedScene prefab, IGameObjectData data, RszImportType importType, Node? parentNode, GameObject? parent = null, int dedupeIndex = 0);

    private sealed class FolderBatch : IBatchContext
    {
        public override string ToString() => folder.Name;

        public readonly List<FolderBatch> folders = new();
        public readonly List<GameObjectBatch> gameObjects = new List<GameObjectBatch>();
        public readonly HashSet<SceneFolder> finishedFolders = new();
        public ScnFile.FolderData? scnData;
        public SceneFolder folder;
        private readonly string? note;
        private readonly ImportContext ctx;

        public int FinishedFolderCount { get; internal set; }

        public FolderBatch(ImportContext importContext, SceneFolder folder, string? note)
        {
            this.ctx = importContext;
            this.folder = folder;
            this.note = note;
        }

        public string Label => $"Importing folder {note}...";
        public bool IsFinished => gameObjects.Count == 0 && folders.Count == 0;

        public int TotalCount => throw new NotImplementedException();

        public (int total, int finished) FolderCount => (folders.Count, FinishedFolderCount);
        public (int total, int finished) ComponentsCount => (0, 0);
        public (int total, int finished) GameObjectCount => (0, 0);

        public async Task AwaitGameObjects(GodotRszImporter converter)
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

    public GodotRszImporter(AssetConfig paths, RszGodotConversionOptions options)
    {
        AssetConfig = paths;
        Options = options;
        fileOption = TypeCache.CreateRszFileOptions(AssetConfig);
    }

    public PackedScene? CreateOrReplaceScene(string sourceFilePath, string importFilepath)
    {
        return CreateOrReplaceSceneResource<SceneFolder>(sourceFilePath, importFilepath);
    }

    public PackedScene? CreateOrReplacePrefab(string sourceFilePath, string importFilepath)
    {
        return CreateOrReplaceSceneResource<PrefabNode>(sourceFilePath, importFilepath);
    }

    private PackedScene? CreateOrReplaceSceneResource<TRoot>(string sourceFilePath, string importFilepath) where TRoot : Node, IRszContainer, new()
    {
        var relativeSourceFile = PathUtils.FullToRelativePath(sourceFilePath, AssetConfig)!;
        var name = PathUtils.GetFilenameWithoutExtensionOrVersion(sourceFilePath);
        var scene = new PackedScene();
        var root = new TRoot() { Game = AssetConfig.Game, Name = name, Asset = new AssetReference(relativeSourceFile) };
        if (root is SceneFolder scn) {
            root.LockNode(true);
            scn.OriginalName = name;
        } else if (root is PrefabNode pfb) {
            pfb.OriginalName = name;
        }
        scene.Pack(root);
        return SaveOrReplaceResource(scene, importFilepath);
    }

    private PackedScene? UpdateSceneResource<TRoot>(TRoot root) where TRoot : Node, IRszContainer, new()
    {
        var relativeSourceFile = root.Asset!.AssetFilename;
        var importFilepath = PathUtils.GetLocalizedImportPath(relativeSourceFile, AssetConfig) ?? throw new Exception("Couldn't resolve import path for resource " + relativeSourceFile);
        var scene = ResourceLoader.Exists(importFilepath) ? ResourceLoader.Load<PackedScene>(importFilepath) : new PackedScene();
        scene.Pack(root);
        return SaveOrReplaceResource(scene, importFilepath);
    }

    private void UpdateProxyPackedScene<TRoot>(TRoot resource, Node rootNode) where TRoot : REResourceProxy, IRszContainer, new()
    {
        var relativeSourceFile = resource.Asset!.AssetFilename;
        var resourceImportPath = PathUtils.GetLocalizedImportPath(relativeSourceFile, AssetConfig) ?? throw new Exception("Couldn't resolve import path for resource " + relativeSourceFile);
        var assetImportPath = PathUtils.GetAssetImportPath(relativeSourceFile, resource.ResourceType, AssetConfig) ?? throw new Exception("Couldn't resolve import path for resource " + relativeSourceFile);
        var scene = ResourceLoader.Exists(assetImportPath) ? ResourceLoader.Load<PackedScene>(assetImportPath) : new PackedScene();
        scene.Pack(rootNode);
        resource.ImportedResource = scene;
        SaveOrReplaceResource(scene, assetImportPath);
        SaveOrReplaceResource(resource, resourceImportPath);
    }

    private TRes? SaveOrReplaceRszResource<TRes>(TRes newResource, string sourceFilePath, string importFilepath) where TRes : Resource, IRszContainer
    {
        if (!File.Exists(sourceFilePath)) {
            GD.PrintErr("Invalid resource source file, does not exist: " + sourceFilePath);
            return null;
        }

        var relativeSourceFile = PathUtils.FullToRelativePath(sourceFilePath, AssetConfig)!;
        var name = PathUtils.GetFilenameWithoutExtensionOrVersion(sourceFilePath);

        newResource.ResourceName = name;
        newResource.ResourcePath = importFilepath;
        newResource.Game = AssetConfig.Game;
        newResource.Asset = new AssetReference(relativeSourceFile);

        return SaveOrReplaceResource(newResource, importFilepath);
    }

    private TRes? SaveOrReplaceResource<TRes>(TRes newResource, string importFilepath) where TRes : Resource
    {
        if (ResourceLoader.Exists(importFilepath)) {
            newResource.TakeOverPath(importFilepath);
        } else {
            Directory.CreateDirectory(ProjectSettings.GlobalizePath(importFilepath.GetBaseDir()));
            newResource.ResourcePath = importFilepath;
        }
        GD.Print(" Saving resource " + importFilepath);
        var status = ResourceSaver.Save(newResource);
        if (status != Error.Ok) {
            GD.PrintErr($"Failed to save resource {importFilepath}:\n{status}");
        }
        ctx.resolvedResources[importFilepath] = newResource;
        Importer.QueueFileRescan();
        return newResource;
    }

    public async Task RegenerateSceneTree(SceneFolder root)
    {
        ctx.Clear();
        var task = GenerateSceneTree(root);
        AsyncImporter.StartAsyncOperation(task, () => ctx.IsCancelled = true);
        try {
            await task;
        } catch (TaskCanceledException) {
            GD.PrintErr("Import cancelled by the user");
        }
        ctx.Clear();
    }

    public async Task RegeneratePrefabTree(PrefabNode root)
    {
        ctx.Clear();
        var task = GeneratePrefabTree(root);
        AsyncImporter.StartAsyncOperation(task, () => ctx.IsCancelled = true);
        try {
            await task;
        } catch (TaskCanceledException) {
            GD.PrintErr("Import cancelled by the user");
        }
        ctx.Clear();
    }

    private async Task GenerateSceneTree(SceneFolder root)
    {
        var fullPath = PathUtils.FindSourceFilePath(root.Asset?.AssetFilename, AssetConfig);
        if (fullPath == null) {
            GD.PrintErr("File not found: " + root.Asset?.AssetFilename);
            return;
        }

        GD.Print("Opening scn file " + fullPath);
        using var file = new ScnFile(fileOption, new FileHandler(fullPath));
        try {
            file.Read();
        } catch (RszRetryOpenException e) {
            e.LogRszRetryException();
            await GenerateSceneTree(root);
            return;
        } catch (Exception e) {
            GD.PrintErr("Failed to parse file " + fullPath, e);
            return;
        }

        file.SetupGameObjects();

        TypeCache.StoreInferredRszTypes(file.RSZ, AssetConfig);

        if (Options.folders == RszImportType.ForceReimport) {
            root.Clear();
        }

        var batch = ctx.CreateFolderBatch(root, null, root.Asset?.AssetFilename);
        ctx.StartBatch(batch);
        GenerateResources(root, file.ResourceInfoList, AssetConfig);
        PrepareFolderBatch(batch, file.GameObjectDatas!, file.FolderDatas!);
        await AwaitFolderBatch(batch);
        ctx.UpdateUIStatus();

        root.RecalculateBounds(true);

        if (root.Owner == null) {
            root.Update = true;
            root.Draw = true;
            root.Active = true;
            ReconstructScnFolderGameObjectRefs(file, root, root);
        }

        ctx.EndBatch(batch);

        GD.Print(" Finished scene tree " + root.Name);
        if (!root.IsInsideTree()) {
            UpdateSceneResource(root);
        }
    }

    private void ReconstructScnFolderGameObjectRefs(ScnFile file, SceneFolder folder, SceneFolder root)
    {
        foreach (var go in folder.ChildObjects.SelectMany(ch => ch.AllChildrenIncludingSelf)) {
            foreach (var comp in go.Components) {
                ReconstructScnGameObjectRefs(comp, comp, go, root);
            }
        }

        foreach (var sub in folder.Subfolders) {
            ReconstructScnFolderGameObjectRefs(file, sub, root);
        }
    }

    private void ReconstructScnGameObjectRefs(REObject obj, REComponent component, GameObject gameobj, SceneFolder root)
    {
        foreach (var field in obj.TypeInfo.Fields) {
            if (field.RszField.type == RszFieldType.GameObjectRef && obj.TryGetFieldValue(field, out var value)) {
                if (field.RszField.array) {
                    var paths = value.AsGodotArray<GameObjectRef>();
                    for (int i = 0; i < paths.Count; ++i) {
                        var path = paths[i];
                        if (ctx.gameObjects.TryGetValue(path.TargetGuid, out var refTarget) && gameobj.Owner == refTarget.Owner) {
                            path.ModifyPathNoCheck(gameobj.GetPathTo(refTarget));
                        } else {
                            // for cross-scn references, we can't guaranteed resolve them so just store the guid without a path
                            path.Path = null;
                        }
                    }
                    obj.SetField(field, paths);
                } else {
                    var path = value.As<GameObjectRef>();
                    if (ctx.gameObjects.TryGetValue(path.TargetGuid, out var refTarget) && gameobj.Owner == refTarget.Owner) {
                        path.ModifyPathNoCheck(gameobj.GetPathTo(refTarget));
                    } else {
                        // for cross-scn references, we can't guaranteed resolve them so just store the guid without a path
                        path.Path = null;
                    }
                }

            } else if (field.RszField.type is RszFieldType.Object) {
                if (!obj.TryGetFieldValue(field, out var child)) continue;

                if (field.RszField.array) {
                    if (child.AsGodotArray<REObject>() is Godot.Collections.Array<REObject> children) {
                        foreach (var childObj in children) {
                            if (childObj != null) {
                                ReconstructScnGameObjectRefs(childObj, component, gameobj, root);
                            }
                        }
                    }
                } else {
                    if (child.As<REObject>() is REObject childObj) {
                        ReconstructScnGameObjectRefs(childObj, component, gameobj, root);
                    }
                }
            }
        }
    }

    private void PrepareFolderBatch(FolderBatch batch, IEnumerable<ScnFile.GameObjectData> gameobjects, IEnumerable<ScnFile.FolderData> folders)
    {
        var dupeDict = new Dictionary<string, int>();
        foreach (var gameObj in gameobjects) {
            Debug.Assert(gameObj.Info != null);


            var childName = gameObj.Name ?? "unnamed";
            if (dupeDict.TryGetValue(childName, out var index)) {
                dupeDict[childName] = ++index;
            } else {
                dupeDict[childName] = index = 0;
            }

            var objBatch = ctx.CreateGameObjectBatch(batch.folder.Path + "/" + childName);
            batch.gameObjects.Add(objBatch);
            PrepareGameObjectBatch(gameObj, Options.prefabs, objBatch, batch.folder, null, index);
        }

        foreach (var folder in folders) {
            Debug.Assert(folder.Info != null);

            PrepareSubfolderPlaceholders(batch.folder, folder, batch.folder, batch);
        }
    }

    private async Task AwaitFolderBatch(FolderBatch batch)
    {
        ctx.StartBatch(batch);
        await ctx.MaybeYield();

        var folder = batch.folder;
        if (folder.Owner != null && folder.Asset?.IsEmpty == false) {
            var folderName = folder.OriginalName;
            var scene = (folder as SceneFolderProxy)?.Contents
                ?? (!string.IsNullOrEmpty(folder.SceneFilePath)
                    ? ResourceLoader.Load<PackedScene>(folder.SceneFilePath)
                    : Importer.FindOrImportResource<PackedScene>(folder.Asset.AssetFilename, AssetConfig));
            int nodeCount = -1;
            var newInstance = scene!.Instantiate<SceneFolder>();
            if (!batch.finishedFolders.Contains(folder)) {
                await GenerateSceneTree(newInstance);
                ctx.UpdateUIStatus();
                folder.KnownBounds = newInstance.KnownBounds;
                scene.Pack(newInstance);
                nodeCount = newInstance.NodeCount;
                var importPath = folder.Asset?.GetImportFilepath(AssetConfig);
                var childFullPath = folder.Asset?.FindSourceFile(AssetConfig);
                batch.finishedFolders.Add(folder);

                if (importPath == null || childFullPath == null) {
                    GD.PrintErr("Invalid folder source file " + folder.Asset?.ToString());
                    return;
                }
            }
            newInstance.OriginalName = folderName;
            if (folder is SceneFolderProxy proxy) {
                proxy.ShowLinkedFolder = true; // TODO configurable by import type?
            } else {
                if (nodeCount == -1) {
                    // not ideal, but I'm not sure this case can even happen
                    nodeCount = scene!.Instantiate<SceneFolder>().NodeCount;
                }
                if (nodeCount > ReachForGodot.SceneFolderProxyThreshold) {
                    batch.folder = folder.ReplaceWithProxy();
                    // tempInstance = tempInstance.ReplaceWithProxy();
                } else {
                    folder.GetParent().EmplaceChild(folder, newInstance);
                }
            }
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
        var fullPath = PathUtils.FindSourceFilePath(root.Asset!.AssetFilename, AssetConfig);
        if (fullPath == null) {
            GD.PrintErr("File not found: " + root.Asset?.AssetFilename);
            return;
        }

        GD.Print("Opening pfb file " + fullPath);
        using var file = new PfbFile(fileOption, new FileHandler(fullPath));
        try {
            file.Read();
            file.SetupGameObjects();
        } catch (Exception e) {
            GD.PrintErr("Failed to parse file " + fullPath, e);
            return;
        }

        GenerateResources(root, file.ResourceInfoList, AssetConfig);
        var batch = ctx.CreatePrefabBatch(root, root.Asset.AssetFilename);

        TypeCache.StoreInferredRszTypes(file.RSZ, AssetConfig);

        if (Options.prefabs == RszImportType.ForceReimport) {
            root.Clear();
        }

        root.Prefab = root.Asset.AssetFilename;
        var rootGOs = file.GameObjectDatas!.OrderBy(o => o.Instance!.Index);
        Debug.Assert(rootGOs.Count() <= 1, "WTF Capcom?? Guess we doing multiple PFB roots now");
        foreach (var gameObj in rootGOs) {
            root.OriginalName = gameObj.Name ?? root.Name;
            PrepareGameObjectBatch(gameObj, Options.prefabs, batch, root);
            await batch.Await(this);
        }

        foreach (var go in root.AllChildrenIncludingSelf) {
            foreach (var comp in go.Components) {
                ReconstructPfbGameObjectRefs(file, comp, comp, root);
            }
        }
    }

    private GameObjectRef? ResolveGameObjectRef(PfbFile file, REField field, REObject obj, RszInstance instance, REComponent component, GameObject root, int arrayIndex)
    {
        // god help me...
        Debug.Assert(instance != null);
        int idx = instance.ObjectTableIndex;

        var fieldRefs = file.GameObjectRefInfoList.Where(rr => rr.Data.objectId == idx && rr.Data.arrayIndex == arrayIndex);
        if (!fieldRefs.Any()) return null;

        var cache = TypeCache.GetClassInfo(AssetConfig.Game, obj.Classname!);
        var propInfoDict = cache.PfbRefs;
        if (!propInfoDict.TryGetValue(field.SerializedName, out var propInfo)) {
            var refValues = file.GameObjectRefInfoList.Where(rr => rr.Data.objectId == idx && rr.Data.arrayIndex == 0).OrderBy(b => b.Data.propertyId);
            var refFields = obj.TypeInfo.Fields.Where(f => f.RszField.type == RszFieldType.GameObjectRef).OrderBy(f => f.FieldIndex);
            if (refFields.Count() == refValues.Count()) {
                // these cases _should_ be trivially inferrable by field order
                propInfoDict = new();
                int i = 0;
                foreach (var propId in refValues.Select(r => r.Data.propertyId)) {
                    var refField = refFields.ElementAt(i++);
                    var prop = new PrefabGameObjectRefProperty() { PropertyId = propId, AutoDetected = true };
                    propInfoDict[refField.SerializedName] = prop;
                    if (refField == field) {
                        propInfo = prop;
                    }
                    GD.PrintErr("Auto-detected GameObjectRef property " + refField.SerializedName + " as propertyId " + propId + ". It may be wrong, but hopefully not.");
                }
                TypeCache.UpdatePfbGameObjectRefCache(AssetConfig.Game, obj.Classname!, propInfoDict);
                Debug.Assert(propInfo != null);
            } else {
                // if any refs from this object do not have a known property Id; this way we only print error if we actually found an unmapped ref
                if (file.GameObjectRefInfoList.Any(info => info.Data.objectId == idx
                    && !propInfoDict.Values.Any(entry => entry.PropertyId == info.Data.propertyId))) {
                    GD.PrintErr("Found undeclared GameObjectRef property " + field.SerializedName + " in class " + obj.Classname + ". If the field had an actual value, it won't be imported correctly.");
                }
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

        return new GameObjectRef(targetGameobj.Uuid, component.GameObject.GetPathTo(targetGameobj));
    }

    private void ReconstructPfbGameObjectRefs(PfbFile file, REObject obj, REComponent component, GameObject root, int arrayIndex = 0)
    {
        RszInstance? instance = null;
        foreach (var field in obj.TypeInfo.Fields) {
            if (field.RszField.type == RszFieldType.GameObjectRef) {
                instance ??= ctx.objectSourceInstances[obj];
                if (field.RszField.array) {
                    var indices = (IList<object>)instance.Values[field.FieldIndex];
                    var paths = new Godot.Collections.Array<GameObjectRef>();
                    for (int i = 0; i < indices.Count; ++i) {
                        var refval = ResolveGameObjectRef(file, field, obj, instance, component, root, i);
                        if (refval == null && (Guid)indices[i] != Guid.Empty) {
                            GD.PrintErr($"Couldn't resolve pfb GameObjectRef node path field {field.SerializedName}[{i}] for {component.Path}");
                        }
                        paths.Add(refval ?? new GameObjectRef());
                    }
                    obj.SetField(field, paths);
                } else {
                    var refval = ResolveGameObjectRef(file, field, obj, instance, component, root, 0);
                    if (refval == null && (Guid)instance.Values[field.FieldIndex] != Guid.Empty) {
                        GD.PrintErr($"Couldn't resolve pfb GameObjectRef node path in field {field.SerializedName} for {component.Path}");
                    }
                    obj.SetField(field, refval ?? new GameObjectRef());
                }
            } else if (field.RszField.type is RszFieldType.Object) {
                if (!obj.TryGetFieldValue(field, out var child)) continue;

                if (field.RszField.array) {
                    if (child.AsGodotArray<REObject>() is Godot.Collections.Array<REObject> children) {
                        int i = 0;
                        foreach (var childObj in children) {
                            if (childObj != null) {
                                ReconstructPfbGameObjectRefs(file, childObj, component, root, i++);
                            }
                        }
                    }
                } else {
                    if (child.As<REObject>() is REObject childObj) {
                        ReconstructPfbGameObjectRefs(file, childObj, component, root);
                    }
                }
            }
        }
    }

    public void GenerateUserdata(UserdataResource root)
    {
        var scnFullPath = PathUtils.FindSourceFilePath(root.Asset!.AssetFilename, AssetConfig);
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
            if (!string.IsNullOrEmpty(root.Classname) && root.Classname != instance.RszClass.name) {
                root.ChangeClassname(instance.RszClass.name);
            } else {
                root.Classname = instance.RszClass.name;
            }
            ApplyObjectValues(root, instance);
            ResourceSaver.Save(root);
            break;
        }
    }

    public void GenerateRcol(RcolResource resource)
    {
        if (Options.userdata == RszImportType.ForceReimport) {
            resource.Clear();
        }

        var root = resource.RcolScene?.Instantiate<RcolRootNode>() ?? new RcolRootNode();
        root.Asset = new AssetReference(resource.Asset!.AssetFilename);
        root.Name = PathUtils.GetFilenameWithoutExtensionOrVersion(resource.Asset.AssetFilename);
        root.Game = AssetConfig.Game;

        GenerateRcol(root);
        UpdateProxyPackedScene(resource, root);
    }

    public void GenerateRcol(RcolRootNode root)
    {
        var fullpath = PathUtils.FindSourceFilePath(root.Asset?.AssetFilename, AssetConfig);
        if (fullpath == null) return;

        GD.Print("Opening rcol file " + fullpath);
        using var file = new RcolFile(fileOption, new FileHandler(fullpath));
        try {
            file.Read();
        } catch (Exception e) {
            GD.PrintErr("Failed to parse file " + fullpath, e);
            return;
        }

        var groupsNode = root.FindChild("Groups");
        if (groupsNode == null) {
            root.AddChild(groupsNode = new Node3D() { Name = "Groups" });
            groupsNode.Owner = root;
        }

        var groupsDict = new Dictionary<Guid, RequestSetCollisionGroup>();
        foreach (var child in groupsNode.FindChildrenByType<RequestSetCollisionGroup>()) {
            groupsDict[child.Guid] = child;
        }

        var setsDict = new Dictionary<uint, RequestSetCollider>();
        foreach (var child in root.FindChildrenByType<RequestSetCollider>()) {
            setsDict[child.ID] = child;
        }

        foreach (var srcGroup in file.GroupInfoList) {
            if (!groupsDict.TryGetValue(srcGroup.Info.guid, out var group)) {
                groupsNode.AddChild(groupsDict[srcGroup.Info.guid] = group = new RequestSetCollisionGroup());
                group.SetDisplayFolded(true);
                group.Owner = root;
                group.Guid = srcGroup.Info.guid;
            }

            group.Name = !string.IsNullOrEmpty(srcGroup.Info.name) ? srcGroup.Info.name : (group.Name ?? srcGroup.Info.guid.ToString());
            group.CollisionMask = srcGroup.Info.MaskBits;
            group.CollisionLayer = (uint)(1 << srcGroup.Info.layerIndex);
            group.MaskGuids = srcGroup.Info.MaskGuids.Select(c => c.ToString()).ToArray();
            group.LayerGuid = srcGroup.Info.layerGuid;

            group.ClearChildren();
            foreach (var srcShape in srcGroup.Shapes) {
                var shapeNode = new RequestSetCollisionShape3D();
                shapeNode.Guid = srcShape.Guid;
                shapeNode.Name = !string.IsNullOrEmpty(srcShape.Name) ? srcShape.Name : shapeNode.Uuid!;
                shapeNode.OriginalName = srcShape.Name;
                shapeNode.PrimaryJointNameStr = srcShape.PrimaryJointNameStr;
                shapeNode.SecondaryJointNameStr = srcShape.SecondaryJointNameStr;
                shapeNode.LayerIndex = srcShape.LayerIndex;
                shapeNode.SkipIdBits = srcShape.SkipIdBits;
                shapeNode.IgnoreTagBits = srcShape.IgnoreTagBits;
                shapeNode.Attribute = srcShape.Attribute;
                shapeNode.Data = srcShape.UserData == null ? null : CreateOrGetObject(srcShape.UserData);
                shapeNode.RcolShapeType = srcShape.shapeType;
                if (srcShape.shape != null) {
                    var fieldType = RequestSetCollisionShape3D.GetShapeFieldType(srcShape.shapeType);
                    RequestSetCollisionShape3D.ApplyShape(shapeNode, srcShape.shapeType, RszTypeConverter.FromRszValueSingleValue(fieldType, srcShape.shape, AssetConfig.Game));
                }
                group.AddUniqueNamedChild(shapeNode);
            }
        }

        foreach (var set in file.RequestSetInfoList) {
            if (!setsDict.TryGetValue(set.id, out var requestSet)) {
                setsDict[set.id] = requestSet = new RequestSetCollider();
                requestSet.Name = !string.IsNullOrEmpty(set.name) ? set.name : ("Set_" + set.id.ToString());
                root.AddUniqueNamedChild(requestSet);
                requestSet.ID = set.id;
                requestSet.OriginalName = set.name;
                requestSet.KeyName = set.keyName;
            }
            if (set.Group != null && groupsDict.TryGetValue(set.Group.Info.guid, out var group)) {
                requestSet.Group = group;
            }
            if (set.Userdata != null) {
                requestSet.Data = CreateOrGetObject(set.Userdata);
            }
        }
    }

    private void GenerateResources(IRszContainer root, List<ResourceInfo> resourceInfos, AssetConfig config)
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
        if (folder.Instance?.GetFieldValue("Path") is string scnPath && !string.IsNullOrWhiteSpace(scnPath)) {
            var isNew = false;
            if (subfolder == null) {
                subfolder = new SceneFolder() { Name = name, OriginalName = name, Asset = new AssetReference(scnPath), Game = AssetConfig.Game };
                subfolder.LockNode(true);
                if (parent.GetNodeOrNull(name) != null) {
                    subfolder.Name = name + "__folder";
                }
                parent.AddFolder(subfolder);
                isNew = true;
            }

            subfolder.OriginalName = name;
            if (folder.Children.Any()) {
                GD.PrintErr($"Unexpected situation: resource-linked scene also has additional children in parent scene.\nParent scene:{root.Asset?.AssetFilename}\nSubfolder:{scnPath}");
            }

            var skipImport = (Options.folders == RszImportType.Placeholders || !isNew && Options.folders == RszImportType.CreateOrReuse);
            if (!skipImport) {
                (subfolder as SceneFolderProxy)?.UnloadScene();
                var newBatch = ctx.CreateFolderBatch(subfolder, folder, scnPath);
                batch.folders.Add(newBatch);
            }
        } else {
            if (subfolder == null) {
                GD.Print("Creating folder " + name);
                subfolder = new SceneFolder() {
                    Game = root.Game,
                    Name = parent.GetNodeOrNull(name) != null ? name + "__folder" : name,
                    OriginalName = name,
                };
                subfolder.LockNode(true);
                parent.AddFolder(subfolder);
            } else {
                if (!string.IsNullOrEmpty(subfolder.SceneFilePath)) {
                    GD.PrintErr($"Found local folder that was also instantiated from a scene - could this be problematic?\nParent scene:{root.Asset?.AssetFilename}\nSubfolder:{name}");
                }
            }

            var newBatch = ctx.CreateFolderBatch(subfolder, folder, subfolder.Path);
            batch.folders.Add(newBatch);
            PrepareFolderBatch(newBatch, folder.GameObjects, folder.Children);
        }

        subfolder.Tag = folder.Instance!.GetFieldValue("Tag") as string;
        subfolder.Update = (byte)folder.Instance!.GetFieldValue("Update")! != 0;
        subfolder.Draw = (byte)folder.Instance!.GetFieldValue("Draw")! != 0;
        subfolder.Active = (byte)folder.Instance!.GetFieldValue("Active")! != 0;
        subfolder.Data = folder.Instance!.GetFieldValue("Data") as byte[];
    }

    private async Task RePrepareBatchedPrefabGameObject(GameObjectBatch batch)
    {
        Debug.Assert(batch.prefabData != null);
        var (prefab, data, importType, parentNode, parentGO, index) = batch.prefabData;
        var importFilepath = PathUtils.GetLocalizedImportPath(prefab.ResourcePath, AssetConfig)!;
        if (!ctx.resolvedResources.TryGetValue(prefab.ResourcePath, out var pfb)) {
            var pfbInstance = prefab.Instantiate<PrefabNode>(PackedScene.GenEditState.Instance);
            await GenerateLinkedPrefabTree(pfbInstance);
        }
        PrepareGameObjectBatch(data, importType, batch, parentNode, parentGO, index);
    }

    private void PrepareGameObjectBatch(IGameObjectData data, RszImportType importType, GameObjectBatch batch, Node? parentNode, GameObject? parent = null, int dedupeIndex = 0)
    {
        var name = data.Name ?? "UnnamedGameObject";

        Debug.Assert(data.Instance != null);

        GameObject? gameobj = batch.GameObject;
        if (gameobj == null && parentNode != null) {
            if (parentNode is GameObject obj) {
                gameobj = obj.GetChild(name, dedupeIndex);
            } else if (parentNode is SceneFolder scn) {
                gameobj = scn.GetGameObject(name, dedupeIndex);
            } else {
                Debug.Assert(false);
            }
            batch.GameObject = gameobj!;
        }

        string? prefabPath = null;

        if (data is ScnFile.GameObjectData scnData) {
            // note: some PFB files aren't shipped with the game, hence the CheckResourceExists check
            // presumably they are only used directly within scn files and not instantiated during runtime
            prefabPath = scnData.Prefab?.Path;
            if (!string.IsNullOrEmpty(prefabPath) && Importer.CheckResourceExists(prefabPath, AssetConfig)) {
                var importFilepath = PathUtils.GetLocalizedImportPath(prefabPath, AssetConfig)!;
                if (!ctx.resolvedResources.ContainsKey(importFilepath) && Importer.FindOrImportResource<PackedScene>(prefabPath, AssetConfig) is PackedScene packedPfb) {
                    if (importType == RszImportType.Placeholders) {
                        var pfbInstance = packedPfb.Instantiate<PrefabNode>(PackedScene.GenEditState.Instance);
                        pfbInstance.Name = name;
                        if (data.Components.FirstOrDefault(t => t.RszClass.name == "via.Transform") is RszInstance transform) {
                            RETransformComponent.ApplyTransform(pfbInstance, transform);
                        }
                        return;
                    }
                    batch.prefabData = new PrefabQueueParams(packedPfb, data, importType, parentNode, parent, dedupeIndex);
                    return;
                }
            }
        }

        var isnew = false;
        if (gameobj == null) {
            isnew = true;
            gameobj = string.IsNullOrEmpty(prefabPath) ? new GameObject() {
                Game = AssetConfig.Game,
                Name = name,
                OriginalName = name,
            } : new PrefabNode() {
                Game = AssetConfig.Game,
                Name = name,
                Prefab = prefabPath,
                OriginalName = name,
            };
        } else {
            gameobj.OriginalName = name;
        }
        batch.GameObject = gameobj;

        if (data is ScnFile.GameObjectData scnData2) {
            var guid = scnData2.Info!.Data.guid;
            gameobj.Uuid = guid.ToString();
            ctx.gameObjects[guid] = gameobj;
        }

        gameobj.Data = CreateOrUpdateObject(data.Instance, gameobj.Data);

        if (gameobj.GetParent() == null && parentNode != null && parentNode != gameobj) {
            parentNode.AddUniqueNamedChild(gameobj);
            var owner = gameobj.FindNodeInParents<Node>(p => p is IRszContainer rsz && rsz.Asset?.IsEmpty == false);
            Debug.Assert(owner != gameobj);
            gameobj.Owner = owner;
            if (gameobj is PrefabNode) {
                owner?.SetEditableInstance(gameobj, true);
            }
        }

        if (importType == RszImportType.Placeholders) {
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
            var childBatch = ctx.CreateGameObjectBatch(gameobj.Path + "/" + childName);
            batch.Children.Add(childBatch);
            PrepareGameObjectBatch(child, importType, childBatch, gameobj, gameobj, index);
        }
        if (isnew && dupeDict.Count == 0) {
            gameobj.SetDisplayFolded(true);
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
        } else if (TypeCache.TryCreateComponent(AssetConfig.Game, classname, gameObject, instance, out componentInfo)) {
            if (componentInfo == null) {
                componentInfo = new REComponentPlaceholder(AssetConfig.Game, classname);
                gameObject.AddComponent(componentInfo);
            } else if (gameObject.GetComponent(classname) == null) {
                // if the component was created but not actually added to the gameobject yet, do so now
                gameObject.AddComponent(componentInfo);
            }
        } else {
            componentInfo = new REComponentPlaceholder(AssetConfig.Game, classname);
            gameObject.AddComponent(componentInfo);
        }

        componentInfo.GameObject = gameObject;
        ApplyObjectValues(componentInfo, instance);
        var setupTask = componentInfo.Setup(instance, Options.meshes);
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
        ctx.objectSourceInstances[obj] = instance;
        return ApplyObjectValues(obj, instance);
    }

    private TREObjectType CreateOrGetObject<TREObjectType>(RszInstance instance) where TREObjectType : REObject, new()
    {
        if (ctx.importedObjects.TryGetValue(instance, out var obj)) {
            return (TREObjectType)obj;
        }

        var newObj = new TREObjectType();
        newObj.Game = AssetConfig.Game;
        newObj.Classname = instance.RszClass.name;
        ctx.importedObjects[instance] = newObj;
        ctx.objectSourceInstances[newObj] = instance;
        ApplyObjectValues(newObj, instance);
        return newObj;
    }

    private REObject ApplyObjectValues(REObject obj, RszInstance instance)
    {
        ctx.importedObjects[instance] = obj;
        ctx.objectSourceInstances[obj] = instance;
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
                    return Importer.FindOrImportResource<Resource>(str, AssetConfig)!;
                } else {
                    return new Variant();
                }
            default:
                return RszTypeConverter.FromRszValueSingleValue(field.RszField.type, value, AssetConfig.Game);
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
                var userdataResource = Importer.FindOrImportResource<UserdataResource>(ud1.Path, AssetConfig)!;
                if (userdataResource != null) {
                    ctx.objectSourceInstances[userdataResource] = rsz;
                    if (userdataResource.IsEmpty && string.IsNullOrEmpty(userdataResource.Classname)) {
                        userdataResource.Classname = rsz.RszClass.name;
                        ResourceSaver.Save(userdataResource);
                    }
                }
                return ctx.importedObjects[rsz] = userdataResource!;
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
