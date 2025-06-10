namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using ReaGE.EFX;
using RszTool;

public class AssetConverter
{
    public AssetConfig AssetConfig { get; private set; }
    public SupportedGame Game
    {
        get => AssetConfig.Game;
        set {
            Context.Clear();
            if (value == AssetConfig?.Game) return;
            AssetConfig = ReachForGodot.GetAssetConfig(value);
            _fileOption = null;
        }
    }

    private static AssetConverter? _instance;
    /// <summary>
    /// A static converter instance intended for general single resource importing.
    /// </summary>
    public static AssetConverter Instance => _instance ??= new AssetConverter(GodotImportOptions.thisFolderOnly);

    public GodotImportOptions Options { get; }

    private ScnConverter? _scn;
    public ScnConverter Scn => _scn ??= new ScnConverter() { Convert = this };

    private PfbConverter? _pfb;
    public PfbConverter Pfb => _pfb ??= new PfbConverter() { Convert = this };

    private RcolConverter? _rcol;
    public RcolConverter Rcol => _rcol ??= new RcolConverter() { Convert = this };

    private UserdataConverter? _user;
    public UserdataConverter User => _user ??= new UserdataConverter() { Convert = this };

    private CdefConverter? _cdef;
    public CdefConverter Cdef => _cdef ??= new CdefConverter() { Convert = this };

    private CfilConverter? _cfil;
    public CfilConverter Cfil => _cfil ??= new CfilConverter() { Convert = this };

    private CmatConverter? _cmat;
    public CmatConverter Cmat => _cmat ??= new CmatConverter() { Convert = this };

    private FoliageConverter? _fol;
    public FoliageConverter Fol => _fol ??= new FoliageConverter() { Convert = this };

    private MeshConverter? _mesh;
    public MeshConverter Mesh => _mesh ??= new MeshConverter() { Convert = this };

    private TextureConverter? _tex;
    public TextureConverter Texture => _tex ??= new TextureConverter() { Convert = this };

    private MdfConverter? _mdf2;
    public MdfConverter Mdf2 => _mdf2 ??= new MdfConverter() { Convert = this };

    private UvarConverter? _uvar;
    public UvarConverter Uvar => _uvar ??= new UvarConverter() { Convert = this };

    private MotbankConverter? _motbank;
    public MotbankConverter Motbank => _motbank ??= new MotbankConverter() { Convert = this };

    private ChfConverter? _chf;
    public ChfConverter Chf => _chf ??= new ChfConverter() { Convert = this };

    private EfxConverter? _efx;
    public EfxConverter Efx => _efx ??= new EfxConverter() { Convert = this };

    private McolConverter? _mcol;
    public McolConverter Mcol => _mcol ??= new McolConverter() { Convert = this };

    private RszFileOption? _fileOption;
    public RszFileOption FileOption => _fileOption ??= TypeCache.CreateRszFileOptions(AssetConfig);

    public bool HasImportedResource(string relativePath) => Context.resolvedResources.ContainsKey(relativePath);
    public Resource? GetImportedResource(string relativePath) => Context.resolvedResources.TryGetValue(relativePath, out var r) ? r : null;
    public bool TryGetImportedResource(string relativePath, out Resource resource) => Context.resolvedResources.TryGetValue(relativePath, out resource!);
    public bool AddResource(string relativePath, Resource r) => Context.resolvedResources.TryAdd(relativePath, r);
    public bool AddResource(REResource r) => Context.resolvedResources.TryAdd(r.Asset!.AssetFilename, r);

    public readonly ImportContext Context = new();

    public AssetConverter(AssetConfig config, GodotImportOptions options)
    {
        AssetConfig = config;
        Options = options;
        Context.ShouldLog = options.logInfo;
    }
    public AssetConverter(SupportedGame game, GodotImportOptions options)
    {
        AssetConfig = ReachForGodot.GetAssetConfig(game);
        Options = options;
        Context.ShouldLog = options.logInfo;
    }

    internal AssetConverter(GodotImportOptions options)
    {
        AssetConfig = null!;
        Options = options;
        Context.ShouldLog = options.logInfo;
    }

    public static AssetConverter InstanceForGame(SupportedGame game)
    {
        Instance.Game = game;
        return Instance;
    }

    public async Task<bool> ImportAssetAsync(IImportableAsset asset, string sourceFilepath)
    {
        var task = ImportAsset(asset, sourceFilepath);
        AsyncImporter.StartAsyncOperation(task, () => Context.IsCancelled = true);
        try {
            return await task;
        } catch (TaskCanceledException) {
            GD.PrintErr("Import cancelled by the user");
            return false;
        }
    }

    public Task<bool> ImportAsset(IImportableAsset asset, AssetConfig config)
    {
        this.AssetConfig = config;
        var source = asset.Asset?.FindSourceFile(config);
        if (string.IsNullOrEmpty(source)) return Task.FromResult(false);
        return ImportAsset(asset, source);
    }

    public Task<bool> ImportAsset(IImportableAsset asset, string filepath)
    {
        if (asset is REResource reres) {
            switch (reres.ResourceType) {
                case SupportedFileFormats.Scene:
                    return Scn.ImportFromFile(filepath, (SceneResource)asset);
                case SupportedFileFormats.Prefab:
                    return Pfb.ImportFromFile(filepath, (PrefabResource)asset);
                case SupportedFileFormats.Rcol:
                    return Rcol.ImportFromFile(filepath, (RcolResource)asset);
                case SupportedFileFormats.Efx:
                    return Efx.ImportFromFile(filepath, (EfxResource)asset);
                case SupportedFileFormats.Userdata:
                    return User.ImportFromFile(filepath, (UserdataResource)asset);
                case SupportedFileFormats.Foliage:
                    return Fol.ImportFromFile(filepath, (FoliageResource)asset);
                case SupportedFileFormats.Mesh:
                    return Mesh.ImportAsset((MeshResource)reres, filepath);
                case SupportedFileFormats.Texture:
                    return Texture.ImportAsset((TextureResource)reres, filepath);
                case SupportedFileFormats.MaterialDefinition:
                    return Mdf2.ImportFromFile(filepath, (MaterialDefinitionResource)asset);
                case SupportedFileFormats.Uvar:
                    return Uvar.ImportFromFile(filepath, (UvarResource)asset);
                case SupportedFileFormats.MotionBank:
                    return Motbank.ImportFromFile(filepath, (MotionBankResource)asset);
                case SupportedFileFormats.CollisionDefinition:
                    return Cdef.ImportFromFile(filepath, (CollisionDefinitionResource)asset);
                case SupportedFileFormats.CollisionFilter:
                    return Cfil.ImportFromFile(filepath, (CollisionFilterResource)asset);
                case SupportedFileFormats.CollisionMaterial:
                    return Cmat.ImportFromFile(filepath, (CollisionMaterialResource)asset);
                case SupportedFileFormats.ColliderHeightField:
                    return Chf.ImportFromFile(filepath, (ColliderHeightFieldResource)asset);
                case SupportedFileFormats.MeshCollider:
                    return Mcol.ImportFromFile(filepath, (MeshColliderResource)asset);
                default:
                    GD.PrintErr("Import currently unsupported for resource type " + reres.ResourceType);
                    return Task.FromResult(false);
            }
        } else if (asset is PrefabNode pfb) {
            return Pfb.ImportFromFileToInstance(filepath, pfb);
        } else if (asset is SceneFolder scn) {
            return Scn.ImportFromFileToInstance(filepath, scn);
        } else if (asset is RcolRootNode rcol) {
            return Rcol.ImportFromFileToInstance(filepath, rcol);
        } else if (asset is EfxRootNode efx) {
            return Efx.ImportFromFileToInstance(filepath, efx);
        } else if (asset is McolRoot mcol) {
            return Task.FromResult(Mcol.ImportFromFile(filepath, mcol, mcol.Resource));
        } else {
            GD.PrintErr("Currently unsupported import for object type " + asset.GetType());
            return Task.FromResult(false);
        }
    }

    public Task<bool> ExportAsset(IExportableAsset resource, string exportBasepath)
    {
        var outputPath = PathUtils.ResolveExportPath(exportBasepath, resource.Asset!.AssetFilename, resource.Game);
        if (string.IsNullOrEmpty(outputPath)) {
            GD.PrintErr("Invalid empty export filepath");
            return Task.FromResult(false);
        }

        Game = resource.Game;

        Directory.CreateDirectory(outputPath.GetBaseDir());
        Context.Clear();
        if (resource is REResource reres) {
            switch (reres.ResourceType) {
                case SupportedFileFormats.Scene:
                    return Scn.ExportToFile(((SceneResource)reres).Instantiate()!, outputPath);
                case SupportedFileFormats.Prefab:
                    return Pfb.ExportToFile(((PrefabResource)reres).Instantiate()!, outputPath);
                case SupportedFileFormats.Rcol:
                    return Rcol.ExportToFile(((RcolResource)reres).Instantiate()!, outputPath);
                case SupportedFileFormats.Efx:
                    return Efx.ExportToFile(((EfxResource)reres).Instantiate()!, outputPath);
                case SupportedFileFormats.MeshCollider:
                    return Mcol.ExportToFile(((MeshColliderResource)reres).Instantiate()!, outputPath);
                case SupportedFileFormats.Userdata:
                    return User.ExportToFile((UserdataResource)reres, outputPath);
                case SupportedFileFormats.Foliage:
                    return Fol.ExportToFile((FoliageResource)reres, outputPath);
                case SupportedFileFormats.MaterialDefinition:
                    return Mdf2.ExportToFile((MaterialDefinitionResource)reres, outputPath);
                case SupportedFileFormats.Uvar:
                    return Uvar.ExportToFile((UvarResource)reres, outputPath);
                case SupportedFileFormats.MotionBank:
                    return Motbank.ExportToFile((MotionBankResource)reres, outputPath);
                case SupportedFileFormats.CollisionDefinition:
                    return Cdef.ExportToFile((CollisionDefinitionResource)reres, outputPath);
                case SupportedFileFormats.CollisionFilter:
                    return Cfil.ExportToFile((CollisionFilterResource)reres, outputPath);
                case SupportedFileFormats.CollisionMaterial:
                    return Cmat.ExportToFile((CollisionMaterialResource)reres, outputPath);
                case SupportedFileFormats.ColliderHeightField:
                    return Chf.ExportToFile((ColliderHeightFieldResource)reres, outputPath);
                default:
                    GD.PrintErr("Currently unsupported export for resource type " + reres.ResourceType);
                    return Task.FromResult(false);
            }
        } else if (resource is PrefabNode pfb) {
            return Pfb.ExportToFile(pfb, outputPath);
        } else if (resource is SceneFolder scn) {
            return Scn.ExportToFile(scn, outputPath);
        } else if (resource is RcolRootNode rcol) {
            return Rcol.ExportToFile(rcol, outputPath);
        } else if (resource is EfxRootNode efx) {
            return Efx.ExportToFile(efx, outputPath);
        } else if (resource is McolRoot mcol) {
            return Mcol.ExportToFile(mcol, outputPath);
        } else {
            GD.PrintErr("Currently unsupported export for object type " + resource.GetType());
            return Task.FromResult(false);
        }
    }

    private static bool PostExport(bool success, string outputFile)
    {
        if (!success && File.Exists(outputFile) && new FileInfo(outputFile).Length == 0) {
            File.Delete(outputFile);
        }

        return success;
    }

    public void StartBatch(IBatchContext batch) => Context.StartBatch(batch);
    public void EndBatch(IBatchContext batch) => Context.EndBatch(batch);
    public GameObjectBatch CreatePrefabBatch(PrefabNode root, string? note) => Context.CreatePrefabBatch(root, note);
    public GameObjectBatch CreateGameObjectBatch(string? note) => Context.CreateGameObjectBatch(note);
    public FolderBatch CreateFolderBatch(SceneFolder folder, RszTool.Scn.ScnFolderData? data, string? note) => Context.CreateFolderBatch(folder, data, note);

#region Import context, batching
    public sealed class ImportContext
    {
        // used to keep track of already imported resources
        public readonly Dictionary<string, Resource> resolvedResources = new();

        public readonly Stack<IBatchContext> pendingBatches = new();
        public readonly List<IBatchContext> batches = new();

        private DateTime lastDateLog;
        private DateTime lastStatusUpdateTime;

        public bool IsCancelled { get; internal set; }
        public bool ShouldLog { get; set; }

        public void Clear()
        {
            resolvedResources.Clear();
            pendingBatches.Clear();
            batches.Clear();
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

        public FolderBatch CreateFolderBatch(SceneFolder folder, RszTool.Scn.ScnFolderData? data, string? note)
        {
            var batch = new FolderBatch(this, folder, note) { scnData = data };
            QueueBatch(batch);
            return batch;
        }

        public void UpdateUIStatus()
        {
            if (!ShouldLog) return;
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

    public sealed class GameObjectBatch : IBatchContext
    {
        public GameObject GameObject = null!;
        public List<Task> ComponentTasks = new();
        public List<GameObjectBatch> Children = new();

        public override string ToString() => GameObject.Owner == null ? GameObject.Name : GameObject.Owner.GetPathTo(GameObject);

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

        public async Task Await(AssetConverter converter)
        {
            ctx.StartBatch(this);
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

    private sealed record PrefabQueueParams(PackedScene prefab, IGameObject data, RszImportType importType, Node? parentNode, GameObject? parent = null, int dedupeIndex = 0);

    public sealed class FolderBatch : IBatchContext
    {
        public override string ToString() => folder.Name;

        public readonly List<FolderBatch> folders = new();
        public readonly List<GameObjectBatch> gameObjects = new List<GameObjectBatch>();
        public readonly HashSet<SceneFolder> finishedFolders = new();
        public RszTool.Scn.ScnFolderData? scnData;
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

        public async Task AwaitGameObjects(AssetConverter converter)
        {
            foreach (var subtask in gameObjects) {
                await subtask.Await(converter);
            }
        }
    }

    private sealed record BatchInfo(string label, IBatchContext status);
    public interface IBatchContext
    {
        string Label { get; }
        bool IsFinished { get; }
        public (int total, int finished) FolderCount { get; }
        public (int total, int finished) ComponentsCount { get; }
        public (int total, int finished) GameObjectCount { get; }
    }
#endregion
}
