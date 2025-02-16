namespace RFG;

using System;
using System.Runtime.Loader;
using Godot;
using RszTool;

public class GodotScnConverter
{
    private static bool hasSafetyHooked;

    public GamePaths Paths { get; }

    public static void EnsureSafeJsonLoadContext()
    {
        if (!hasSafetyHooked && Engine.IsEditorHint()) {
            hasSafetyHooked = true;
            AssemblyLoadContext.GetLoadContext(typeof(GodotScnConverter).Assembly)!.Unloading += (c) => {
                var assembly = typeof(System.Text.Json.JsonSerializerOptions).Assembly;
                var updateHandlerType = assembly.GetType("System.Text.Json.JsonSerializerOptionsUpdateHandler");
                var clearCacheMethod = updateHandlerType?.GetMethod("ClearCache", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                clearCacheMethod!.Invoke(null, new object?[] { null });
            };
        }
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
        using var scn = OpenScn(scnFullPath);
        scn.Read();
        scn.SetupGameObjects();

        root.FreeAllChildren();

        if (scn.FolderDatas != null) {
            foreach (var folder in scn.FolderDatas) {
                if (folder.Info != null) {
                    if (folder.Info.Data.parentId == -1) {
                        var folderNode = new REFolder() {
                            ObjectId = folder.Info.Data.objectId,
                            Name = folder.Name ?? "UnnamedFolder"
                        };
                        root.AddChild(folderNode);
                        folderNode.Owner = root;
                    }
                }
            }
        }
        // foreach (var fi in scn.FolderInfoList) {
        //     GD.Print("folder info " + fi.Data.objectId);
        // }
    }

    private ScnFile OpenScn(string filename)
    {
        return new ScnFile(new RszFileOption(ConvertGameToRszToolGame(Paths.Game), Paths.RszJsonPath), new FileHandler(filename));
    }

    public static GameName ConvertGameToRszToolGame(string game)
    {
        switch (game) {
            case "DragonsDogma2": return GameName.dd2;
            default: return GameName.unknown;
        }
    }
}
