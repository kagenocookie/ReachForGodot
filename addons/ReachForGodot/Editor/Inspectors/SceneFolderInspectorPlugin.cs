#if TOOLS
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;

namespace RGE;

public partial class SceneFolderInspectorPlugin : EditorInspectorPlugin, ISerializationListener
{
    private static PluginSerializationFixer pluginSerializationFixer = new();

    public void OnAfterDeserialize() { }
    public void OnBeforeSerialize() => pluginSerializationFixer.OnBeforeSerialize();

    private PackedScene? inspectorScene;

    public override bool _CanHandle(GodotObject @object)
    {
        return @object is SceneFolder or PrefabNode;
    }

    public override void _ParseBegin(GodotObject @object)
    {
        if (@object is IRszContainerNode rsz) {
            CreateUI(rsz);
        }
    }

    private void CreateUI(IRszContainerNode obj)
    {
        inspectorScene ??= ResourceLoader.Load<PackedScene>("res://addons/ReachForGodot/Editor/Inspectors/SceneFolderInspector.tscn");
        var container = inspectorScene.Instantiate<Control>();

        if (container.GetNode<Button>("%Find3DNode") is Button btn2) {
            if (obj is SceneFolder folder) {
                btn2.Pressed += folder.EditorRepositionCamera;
            } else {
                btn2.Visible = false;
            }
        }
        if (container.GetNode<Button>("%LoadFoldersBtn") is Button loadbtn) {
            if (obj is SceneFolder scene) {
                loadbtn.Pressed += () => {
                    bool? targetValue = null;
                    if (scene is SceneFolderProxy proxy) {
                        proxy.LoadAllChildren(!proxy.ShowLinkedFolder);
                    } else {
                        foreach (var ch in scene.AllSubfolders.OfType<SceneFolderProxy>()) {
                            if (targetValue == null) {
                                targetValue = !ch.ShowLinkedFolder;
                            }
                            ch.LoadAllChildren(targetValue.Value);
                        }
                    }
                };
            } else {
                loadbtn.Visible = false;
            }
        }

        if (container.GetNode<Button>("%ConvertSceneToProxy") is Button proxyBtn) {
            if (obj is SceneFolder folder && folder is not SceneFolderProxy && folder.GetParent() != null && folder.GetParent() is not SceneFolderProxy && folder.Owner != null) {
                proxyBtn.Pressed += () => {
                    var parent = folder.GetParent();
                    var index = folder.GetIndex();
                    var proxy = new SceneFolderProxy() {
                        Game = folder.Game,
                        OriginalName = folder.OriginalName,
                        Asset = new AssetReference(folder.Asset!.AssetFilename),
                        KnownBounds = folder.KnownBounds,
                    };
                    parent.AddChild(proxy);
                    parent.MoveChild(proxy, index);
                    folder.Reparent(proxy);
                    proxy.Owner = folder.Owner;
                    proxy.Name = folder.Name;
                    proxy.ShowLinkedFolder = true;
                };
            } else {
                proxyBtn.Visible = false;
            }
        }

        if (container.GetNode<Button>("%RecalcBounds") is Button recalcBtn) {
            if (obj is SceneFolder scene) {
                recalcBtn.Pressed += () => {
                    scene.RecalculateBounds(true);
                    EditorInterface.Singleton.MarkSceneAsUnsaved();
                };
            } else if (obj is PrefabNode) {
                recalcBtn.Visible = false;
            }
        }

        var importType = container.GetNode<OptionButton>("%ImportTypeOption");
        importType.Clear();
        if (obj is PrefabNode pfb) {
            importType.AddItem("Import anything missing", (int)GodotRszImporter.PresetImportModes.ImportTreeChanges);
            importType.AddItem("Discard and reimport structure", (int)GodotRszImporter.PresetImportModes.ReimportStructure);
            importType.AddItem("Fully reimport all resources", (int)GodotRszImporter.PresetImportModes.FullReimport);
        } else if (obj is SceneFolder scn) {
            importType.AddItem("Placeholders only", (int)GodotRszImporter.PresetImportModes.PlaceholderImport);
            importType.AddItem("Import just this scene, no subfolders", (int)GodotRszImporter.PresetImportModes.ThisFolderOnly);
            importType.AddItem("Import missing objects", (int)GodotRszImporter.PresetImportModes.ImportMissingItems);
            importType.AddItem("Discard and reimport scene structure", (int)GodotRszImporter.PresetImportModes.ReimportStructure);
            importType.AddItem("Force reimport all resources", (int)GodotRszImporter.PresetImportModes.FullReimport);
        } else {
            importType.AddItem("Full import", (int)GodotRszImporter.PresetImportModes.ImportTreeChanges);
        }

        var fileSources = PathUtils.FindFileSourceFolders(obj.Asset?.AssetFilename, ReachForGodot.GetAssetConfig(obj.Game)).ToArray();
        var sourcesContainer = container.GetNode<Control>("%ImportSourceContainer");
        var sourceOption = sourcesContainer.RequireChildByType<OptionButton>();
        if (fileSources.Length > 1) {
            sourcesContainer.Visible = true;
            sourceOption.Clear();
            foreach (var src in fileSources) {
                sourceOption.AddItem(src.label);
            }
        } else {
            sourcesContainer.Visible = false;
        }

        var importBtn = container.GetNode<Button>("%ImportButton");
        importBtn.Pressed += async () => {
            var source = sourceOption.Selected == -1 ? fileSources.First() : fileSources[sourceOption.Selected];
            var config = ReachForGodot.GetAssetConfig(obj.Game);
            config.Paths.SourcePathOverride = source;
            try {
                var options = ((GodotRszImporter.PresetImportModes)importType.GetSelectedId()).ToOptions();
                await DoRebuild(obj, options);
            } finally {
                config.Paths.SourcePathOverride = null;
            }
        };

        AddCustomControl(container);
        pluginSerializationFixer.Register((GodotObject)obj, container);
    }

    private async Task DoRebuild(IRszContainerNode root, RszGodotConversionOptions options)
    {
        var sw = new Stopwatch();
        sw.Start();

        var conv = new GodotRszImporter(ReachForGodot.GetAssetConfig(root.Game), options);

        Task task;
        if (root is PrefabNode pfb) {
            task = conv.RegeneratePrefabTree(pfb);
        } else if (root is SceneFolder scn) {
            if (scn.GetParent() is SceneFolderProxy parentProxy) {
                root = scn = parentProxy;
            }
            if (scn is SceneFolderProxy proxy) {
                var realScene = proxy.Contents;
                scn = realScene!.Instantiate<SceneFolder>();
                proxy.UnloadScene();
            }
            task = conv.RegenerateSceneTree(scn);
        } else {
            GD.PrintErr("I have no idea how to import a " + root.GetType());
            return;
        }

        try {
            await task;
            GD.Print("Tree rebuild finished in " + sw.Elapsed);
            if (root is SceneFolderProxy proxy) {
                if (proxy.ShowLinkedFolder) {
                    proxy.LoadScene();
                }
            } else {
                EditorInterface.Singleton.CallDeferred(EditorInterface.MethodName.MarkSceneAsUnsaved);
            }
        } catch (Exception e) {
            GD.Print("Tree rebuild failed:", e);
        }
    }
}
#endif