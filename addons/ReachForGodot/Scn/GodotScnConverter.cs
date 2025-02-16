namespace RFG;

using System;
using System.Diagnostics;
using System.Runtime.Loader;
using Godot;
using RszTool;

public class GodotScnConverter : IDisposable
{
    private static bool hasSafetyHooked;

    private static readonly Dictionary<string, Func<ScnFile, REGameObject, RszInstance, Node?>> factories = new();

    public GamePaths Paths { get; }
    public ScnFile? ScnFile { get; private set; }

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

    public GodotScnConverter(GamePaths paths)
    {
        Paths = paths;
    }

    public void CreateProxyScene(string sourceScnFile, string outputSceneFile)
    {
        EnsureSafeJsonLoadContext();
        var relativeSource = string.IsNullOrEmpty(Paths.ChunkPath) ? sourceScnFile : sourceScnFile.Replace(Paths.ChunkPath, "");

        Directory.CreateDirectory(outputSceneFile.GetBaseDir());
        var localizedOutput = ProjectSettings.LocalizePath(outputSceneFile);

        var name = sourceScnFile.GetFile().GetBaseName().GetBaseName();
        // opening the scene file mostly just to verify that it's valid
        using var scn = OpenScn(sourceScnFile);
        scn.Read();

        if (!ResourceLoader.Exists(localizedOutput)) {
            var scene = new PackedScene();
            scene.Pack(new SceneNodeRoot() { Name = name, Asset = new AssetReference() { AssetFilename = relativeSource } });
            ResourceSaver.Save(scene, localizedOutput);
        }

        EditorInterface.Singleton.CallDeferred(EditorInterface.MethodName.OpenSceneFromPath, localizedOutput);
    }

    public void GenerateSceneTree(SceneNodeRoot root)
    {
        EnsureSafeJsonLoadContext();
        var scnFullPath = Path.Combine(Paths.ChunkPath, root.Asset!.AssetFilename);

        GD.Print("Opening scn file " + scnFullPath);
        ScnFile = OpenScn(scnFullPath);
        ScnFile.Read();
        ScnFile.SetupGameObjects();

        root.Clear();

        foreach (var folder in ScnFile.FolderDatas!.OrderBy(o => o.Instance!.Index)) {
            Debug.Assert(folder.Info != null);
            GenerateFolder(ScnFile, folder, root);
        }

        root.Resources = ScnFile.ResourceInfoList.Where(rr => rr.Path != null).Select(rr => rr.Path!).ToArray();

        foreach (var gameObj in ScnFile.GameObjectDatas!.OrderBy(o => o.Instance!.Index)) {
            Debug.Assert(gameObj.Info != null);
            GenerateGameObject(ScnFile, gameObj, root);
        }
    }

    private void GenerateFolder(ScnFile file, ScnFile.FolderData folder, SceneNodeRoot root, REFolder? parent = null)
    {
        Debug.Assert(folder.Info != null);
        var newFolder = new REFolder() {
            ObjectId = folder.Info.Data.objectId,
            Name = folder.Name ?? "UnnamedFolder"
        };
        root.AddFolder(newFolder, parent);

        foreach (var child in folder.Children) {
            GenerateFolder(file, child, root, newFolder);
        }
    }

    private void GenerateGameObject(ScnFile file, ScnFile.GameObjectData data, SceneNodeRoot root, REGameObject? parent = null)
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
            GenerateGameObject(file, child, root, newGameobj);
        }

        var meshComponent = data.Components.FirstOrDefault(c => c.RszClass.name == "via.render.Mesh" || c.RszClass.name == "via.render.CompositeMesh");
        if (meshComponent != null) {
            newGameobj.Root3D = SetupComponent(meshComponent, newGameobj) as Node3D;
        }

        foreach (var comp in data.Components.OrderBy(o => o.Index)) {
            if (comp != meshComponent) {
                SetupComponent(comp, newGameobj);
            }
        }
    }

    private ScnFile OpenScn(string filename)
    {
        return new ScnFile(new RszFileOption(Paths.GetRszToolGameEnum(), Paths.RszJsonPath ?? throw new Exception("Rsz json file not specified for game " + Paths.Game)), new FileHandler(filename));
    }

    private Node SetupComponent(RszInstance instance, REGameObject gameObject)
    {
        Debug.Assert(ScnFile != null);
        REComponentPlaceholder? componentInfo;
        Node? child;
        if (factories.TryGetValue(instance.RszClass.name, out var factory)) {
            child = factory.Invoke(ScnFile, gameObject, instance);
            componentInfo = child as REComponentPlaceholder;
            if (componentInfo != null) {
                child = componentInfo;
            } else if (child != null) {
                child.AddOwnedChild(componentInfo = new REComponentPlaceholder() { Name = "ComponentInfo" });
            } else {
                child = componentInfo = gameObject.AddOwnedChild(new REComponentPlaceholder() { Name = instance.RszClass.name });
            }
        } else {
            child = componentInfo = gameObject.AddOwnedChild(new REComponentPlaceholder() { Name = instance.RszClass.name });
        }

        componentInfo.Classname = instance.RszClass.name;
        componentInfo.ObjectId = instance.Index;
        return child;
    }

    public static void DefineComponentFactory(string componentType, Func<ScnFile, REGameObject, RszInstance, Node?> factory)
    {
        factories[componentType] = factory;
    }

    public void Dispose()
    {
        ScnFile?.Dispose();
        GC.SuppressFinalize(this);
    }
}
