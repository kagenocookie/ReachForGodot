namespace RGE;

using System;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Godot;
using RszTool;

public static class PresetImportModeExtensions
{
    public static RszGodotConversionOptions ToOptions(this RszGodotConverter.PresetImportModes mode) => mode switch {
        RszGodotConverter.PresetImportModes.PlaceholderImport => RszGodotConverter.placeholderImport,
        RszGodotConverter.PresetImportModes.ThisFolderOnly => RszGodotConverter.thisFolderOnly,
        RszGodotConverter.PresetImportModes.ImportMissingItems => RszGodotConverter.importMissing,
        RszGodotConverter.PresetImportModes.ImportTreeChanges => RszGodotConverter.importTreeChanges,
        RszGodotConverter.PresetImportModes.FullReimport => RszGodotConverter.fullReimport,
        _ => RszGodotConverter.placeholderImport,
    };
}

public class RszGodotConverter
{
    public static readonly RszGodotConversionOptions placeholderImport = new(RszImportType.Placeholders, RszImportType.Placeholders, RszImportType.Placeholders, RszImportType.Placeholders);
    public static readonly RszGodotConversionOptions thisFolderOnly = new(RszImportType.Placeholders, RszImportType.Import, RszImportType.Import, RszImportType.Import);
    public static readonly RszGodotConversionOptions importMissing = new(RszImportType.Import, RszImportType.Import, RszImportType.Import, RszImportType.Import);
    public static readonly RszGodotConversionOptions importTreeChanges = new(RszImportType.Reimport, RszImportType.Reimport, RszImportType.Import, RszImportType.Reimport);
    public static readonly RszGodotConversionOptions fullReimport = new(RszImportType.ForceReimport, RszImportType.ForceReimport, RszImportType.ForceReimport, RszImportType.ForceReimport);

    private static readonly Dictionary<SupportedGame, Dictionary<string, Func<IRszContainerNode, REGameObject, RszInstance, REComponent?>>> perGameFactories = new();

    public enum PresetImportModes
    {
        PlaceholderImport = 0,
        ThisFolderOnly,
        ImportMissingItems,
        ImportTreeChanges,
        FullReimport
    }

    public AssetConfig AssetConfig { get; }
    public RszGodotConversionOptions Options { get; }

    private RszFileOption fileOption;

    private readonly Dictionary<string, Resource?> resolvedResources = new();

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

            var attr = type.GetCustomAttribute<REComponentClassAttribute>()!;
            DefineComponentFactory(attr.Classname, (root, obj, instance) => {
                var node = (REComponent)Activator.CreateInstance(type)!;
                return node;
            }, attr.SupportedGames);
        }
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
    private PackedScene? UpdateSceneResource<TRoot>(TRoot root, string sourceFilePath, string importFilepath) where TRoot : Node, IRszContainerNode, new()
    {
        var relativeSourceFile = AssetConfig.Paths.GetChunkRelativePath(sourceFilePath);
        var name = sourceFilePath.GetFile().GetBaseName().GetBaseName();
        var scene = new PackedScene();
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
            resolvedResources[sourceFilePath] = null;
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
        resolvedResources[importFilepath] = newResource;
        return newResource;
    }

    private readonly Dictionary<RszInstance, REObject> importedObjects = new();

    public async Task GenerateSceneTree(SceneFolder root)
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

        if (Options.folders == RszImportType.ForceReimport) {
            root.ClearChildren();
        }

        file.SetupGameObjects();

        GenerateResources(root, file.ResourceInfoList, AssetConfig);

        foreach (var folder in file.FolderDatas!.OrderBy(o => o.Instance!.Index)) {
            Debug.Assert(folder.Info != null);
            PrepareSubfolderPlaceholders(root, folder, root);
        }

        GD.Print("Gameobject count: " + file.GameObjectInfoList.Count);
        foreach (var gameObj in file.GameObjectDatas!.OrderBy(o => o.Instance!.Index)) {
            Debug.Assert(gameObj.Info != null);
            await GenerateGameObject(root, gameObj, Options.folders);
        }

        if (Options.folders >= RszImportType.Import) {
            await GenerateSubfolders(root);
        }

        GD.Print(" Finished scene tree " + root.Name);
    }

    private async Task GenerateSubfolders(SceneFolder folder)
    {
        if (folder.FolderContainer == null) return;

        foreach (var subfolder in folder.Subfolders.ToArray()) {
            var scnPath = subfolder.Asset!.AssetFilename;
            var importPath = subfolder.Asset!.GetImportFilepath(AssetConfig) ?? throw new Exception("Invalid scn import path");
            var scnFullPath = Importer.ResolveSourceFilePath(subfolder.Asset!.AssetFilename, AssetConfig) ?? throw new Exception("Invalid scn file " + scnPath);
            var tempInstance = Importer.FindOrImportResource<PackedScene>(scnFullPath, AssetConfig)!.Instantiate<SceneFolder>();
            // update the packed scene with a separate instance instead of doing it directly on the existing instance
            await GenerateSceneTree(tempInstance);
            UpdateSceneResource(tempInstance, scnFullPath, importPath);
        }
    }

    private async Task GenerateLinkedPrefabTree(PrefabNode root)
    {
        await GeneratePrefabTree(root);
        var scnFullPath = Importer.ResolveSourceFilePath(root.Asset!.AssetFilename, AssetConfig);
        if (scnFullPath == null) return;
        var importPath = Importer.GetLocalizedImportPath(scnFullPath, AssetConfig) ?? throw new Exception("Invalid pfb import path");
        UpdateSceneResource(root, scnFullPath, importPath);
    }

    public async Task GeneratePrefabTree(PrefabNode root)
    {
        var scnFullPath = Importer.ResolveSourceFilePath(root.Asset!.AssetFilename, AssetConfig);
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

        if (Options.prefabs == RszImportType.ForceReimport) {
            root.ClearChildren();
            root.Components.Clear();
        }

        importedObjects.Clear();
        var rootGOs = file.GameObjectDatas!.OrderBy(o => o.Instance!.Index);
        Debug.Assert(rootGOs.Count() <= 1, "WTF Capcom. Guess we doing multiple PFB roots now");
        foreach (var gameObj in rootGOs) {
            root.OriginalName = gameObj.Name ?? root.Name;
            await GenerateGameObject(root, gameObj, Options.prefabs);
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
        instance ??= importedObjects.FirstOrDefault(kv => kv.Value == obj).Key;
        Debug.Assert(instance != null);
        int idx = instance.ObjectTableIndex;

        var fieldRefs = file.GameObjectRefInfoList.Where(rr => rr.Data.objectId == idx && rr.Data.arrayIndex == arrayIndex);
        if (!fieldRefs.Any()) return null;

        var propInfoDict = TypeCache.GetData(AssetConfig.Game, obj.Classname!).PfbRefs;
        if (!propInfoDict.TryGetValue(field.SerializedName, out var propInfo)) {
            GD.PrintErr("Found undeclared GameObjectRef property " + field.SerializedName);
            return default;
        }

        var objref = fieldRefs.FirstOrDefault(rr => rr.Data.propertyId == propInfo.PropertyId);
        if (objref == null) {
            GD.PrintErr("Could not match GameObjectRef field ref");
            return default;
        }

        // TODO handle array index refs
        if (objref.Data.arrayIndex != 0) {
            GD.PrintErr("Please verify that array GameObjectRef resolved correctly; expected array index: " + arrayIndex + " component: " + component);
        }

        var targetInstance = file.RSZ?.ObjectList[(int)objref.Data.targetId];
        if (targetInstance == null) {
            GD.PrintErr("GameObjectRef target object not found");
            return default;
        }

        if (!importedObjects.TryGetValue(targetInstance, out var targetGameobjData)) {
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
                    GD.PrintErr("GameObjectRef array import currently unsupported!! " + component.Classname + ": " + root.GetPathTo(gameobj));
                    // if (child.AsGodotArray<NodePath>() is Godot.Collections.Array<NodePath> children) {
                    //     int i = 0;
                    //     foreach (var childObj in children) {
                    //         if (childObj != null) {
                    //             ReconstructGameObjectRefs(file, childObj, component, gameobj, root, i++);
                    //         }
                    //     }
                    // }
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
        var scnFullPath = Importer.ResolveSourceFilePath(root.Asset!.AssetFilename, AssetConfig);
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
                    resource ??= new REResource() {
                        Asset = new AssetReference(res.Path),
                        ResourceType = Importer.GetFileFormat(res.Path).format,
                        Game = AssetConfig.Game,
                        ResourceName = res.Path.GetFile()
                    };
                } else if (resource is REResource reres) {
                    resources.Add(reres);
                } else {
                    resources.Add(new REResourceProxy() {
                        Asset = new AssetReference(res.Path),
                        ResourceType = Importer.GetFileFormat(res.Path).format,
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

    private void PrepareSubfolderPlaceholders(SceneFolder root, ScnFile.FolderData folder, SceneFolder parent)
    {
        Debug.Assert(folder.Info != null);
        var name = folder.Name ?? "UnnamedFolder";
        if (folder.Instance?.GetFieldValue("v5") is string scnPath && !string.IsNullOrWhiteSpace(scnPath)) {
            var importPath = Importer.GetLocalizedImportPath(scnPath, AssetConfig);
            GD.Print("Importing folder " + scnPath);
            if (importPath == null) {
                GD.PrintErr("Missing scene file " + scnPath);
                if (parent.GetFolder(name) == null) {
                    parent.AddFolder(new SceneFolder() { Name = name });
                }
                return;
            }

            var subfolder = parent.GetFolder(name);
            if (subfolder == null || Options.folders == RszImportType.ForceReimport) {
                if (subfolder != null) parent.RemoveFolder(subfolder);
                if (!ResourceLoader.Exists(importPath) || Options.folders == RszImportType.ForceReimport) {
                    var scene = CreateOrReplaceScene(Importer.ResolveSourceFilePath(scnPath, AssetConfig)!, importPath)!;
                    subfolder = scene.Instantiate<SceneFolder>(PackedScene.GenEditState.Instance);
                } else {
                    var scene = ResourceLoader.Load<PackedScene>(importPath);
                    subfolder = scene.Instantiate<SceneFolder>(PackedScene.GenEditState.Instance);
                }
                parent.AddFolder(subfolder);
            }

            if (subfolder.Name != name) {
                GD.PrintErr("Parent and child scene name mismatch? " + subfolder.Name + " => " + name);
            }
            subfolder.Name = name;
            if (folder.Children.Any()) {
                GD.PrintErr($"Unexpected situation: resource-linked scene also has additional children in parent scene.\nParent scene:{root.Asset?.AssetFilename}\nSubfolder:{scnPath}");
            }
        } else {
            var subfolder = parent.GetFolder(name);
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

            foreach (var child in folder.Children) {
                PrepareSubfolderPlaceholders(root, child, subfolder);
            }
        }
    }

    private async Task GenerateGameObject(IRszContainerNode root, IGameObjectData data, RszImportType importType, REGameObject? parent = null, int dedupeIndex = 0)
    {
        var name = data.Name ?? "UnnamedGameObject";

        Debug.Assert(data.Instance != null);

        string? uuid = null;
        var gameobj = parent == null ? root as PrefabNode : (parent ?? root as REGameObject)?.GetChild(name, dedupeIndex);

        if (data is ScnFile.GameObjectData scnData) {
            uuid = scnData.Info?.Data.guid.ToString();

            // note: some PFB files aren't shipped with the game, hence the CheckResourceExists check
            // presumably they are only used directly within scn files and not instantiated during runtime
            if (!string.IsNullOrEmpty(scnData.Prefab?.Path) && Importer.CheckResourceExists(scnData.Prefab.Path, AssetConfig)) {
                if (resolvedResources.TryGetValue(Importer.GetLocalizedImportPath(scnData.Prefab.Path, AssetConfig)!, out var pfb) && pfb is PackedScene resolvedPfb) {
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
                    await GenerateLinkedPrefabTree(pfbInstance);
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

        if (uuid != null) {
            gameobj.Uuid = uuid;
        }

        gameobj.Data = CreateOrUpdateObject(data.Instance, gameobj.Data);

        if (gameobj.GetParent() == null) {
            root.AddGameObject(gameobj, parent);
        }

        foreach (var comp in data.Components.OrderBy(o => o.Index)) {
            await SetupComponent(root, comp, gameobj, importType);
        }

        var dupeDict = new Dictionary<string, int>();
        foreach (var child in data.GetChildren().OrderBy(o => o.Instance!.Index)) {
            var childName = child.Name ?? "unnamed";
            if (dupeDict.TryGetValue(childName, out var index)) {
                dupeDict[childName] = ++index;
            } else {
                dupeDict[childName] = index = 0;
            }
            await GenerateGameObject(root, child, importType, gameobj, index);
        }
    }

    private async Task SetupComponent(IRszContainerNode root, RszInstance instance, REGameObject gameObject, RszImportType importType)
    {
        if (root.Game == SupportedGame.Unknown) {
            GD.PrintErr("Game required on rsz container root for SetupComponent");
            return;
        }

        var classname = instance.RszClass.name;
        var componentInfo = gameObject.GetComponent(classname);
        if (componentInfo != null) {
            // nothing to do here
        } else if (
            perGameFactories.TryGetValue(root.Game, out var factories) &&
            factories.TryGetValue(classname, out var factory)
        ) {
            componentInfo = factory.Invoke(root, gameObject, instance);
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
        await componentInfo.Setup(root, gameObject, instance, Options.meshes);
    }

    private REObject CreateOrUpdateObject(RszInstance instance, REObject? obj)
    {
        return obj == null ? CreateOrGetObject(instance) : ApplyObjectValues(obj, instance);
    }

    private REObject CreateOrGetObject(RszInstance instance)
    {
        if (importedObjects.TryGetValue(instance, out var obj)) {
            return obj;
        }

        obj = new REObject(AssetConfig.Game, instance.RszClass.name);
        importedObjects[instance] = obj;
        return ApplyObjectValues(obj, instance);
    }

    private REObject ApplyObjectValues(REObject obj, RszInstance instance)
    {
        importedObjects[instance] = obj;
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
        if (importedObjects.TryGetValue(rsz, out var previousInst)) {
            return previousInst;
        }
        if (rsz.Index == 0) return new Variant();

        if (rsz.RSZUserData is RSZUserDataInfo ud1) {
            if (!string.IsNullOrEmpty(ud1.Path)) {
                return importedObjects[rsz] = Importer.FindOrImportResource<UserdataResource>(ud1.Path, AssetConfig)!;
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
    RszImportType prefabs = RszImportType.Import,
    RszImportType meshes = RszImportType.Import,
    RszImportType userdata = RszImportType.Placeholders
);

public enum RszImportType
{
    /// <summary>If an asset does not exist, only create a placeholder resource for it.</summary>
    Placeholders,
    /// <summary>If an asset does not exist or is merely a placeholder, import and generate its data. Do nothing if any of its contents are already imported.</summary>
    Import,
    /// <summary>Reimport the full asset from the source file, maintaining any local changes as much as possible.</summary>
    Reimport,
    /// <summary>Discard any local data and regenerate assets.</summary>
    ForceReimport,
}
