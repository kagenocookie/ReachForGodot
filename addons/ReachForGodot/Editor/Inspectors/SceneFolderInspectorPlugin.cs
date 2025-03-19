#if TOOLS
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;
using ReaGE.EditorLogic;

namespace ReaGE;

public partial class SceneFolderInspectorPlugin : EditorInspectorPlugin, ISerializationListener
{
    private static PluginSerializationFixer pluginSerializationFixer = new();

    public void OnAfterDeserialize() { }
    public void OnBeforeSerialize() => pluginSerializationFixer.OnBeforeSerialize();

    private PackedScene? inspectorScene;

    private static int lastSelectedSceneImport = 0;
    private static int lastSelectedPrefabImport = 0;

    public override bool _CanHandle(GodotObject @object)
    {
        return @object is Node and IAssetPointer;
    }

    public override void _ParseBegin(GodotObject @object)
    {
        if (@object is IAssetPointer rsz) {
            CreateUI(rsz);
        }
    }

    private void CreateUI(IAssetPointer obj)
    {
        inspectorScene ??= ResourceLoader.Load<PackedScene>("res://addons/ReachForGodot/Editor/Inspectors/SceneFolderInspector.tscn");
        var container = inspectorScene.Instantiate<Control>();

        if (container.GetNode<Button>("%RecalcBounds") is Button recalcBtn) {
            if (obj is SceneFolder scene) {
                recalcBtn.Pressed += () => {
                    scene.RecalculateBounds(true);
                    EditorInterface.Singleton.MarkSceneAsUnsaved();
                };
            } else {
                recalcBtn.Visible = false;
            }
        }

        if (container.GetNode<Button>("%ConvertSceneToProxy") is Button proxyBtn) {
            if (obj is SceneFolder folder && folder is not SceneFolderProxy &&
                folder.GetParent() != null && folder.GetParent() is not SceneFolderProxy && folder.Owner != null && folder.Asset?.AssetFilename != null) {
                proxyBtn.Pressed += () => {
                    new MakeProxyFolderAction(folder).TriggerAndSelectNode();
                };
            } else {
                proxyBtn.Visible = false;
            }
        }

        if (container.GetNode<Button>("%CancelSceneProxy") is Button deproxyBtn) {
            if (obj is SceneFolderProxy proxy) {
                deproxyBtn.Pressed += () => {
                    new MakeProxyFolderAction(proxy).TriggerAndSelectNode();
                };
            } else {
                deproxyBtn.Visible = false;
            }
        }

        if (container.GetNode<Button>("%SaveEditableInstance") is Button revertEditable) {
            if (obj is SceneFolder instanceScene and not SceneFolderProxy && instanceScene.Owner != null && instanceScene.Owner.IsEditableInstance(instanceScene)) {
                revertEditable.Pressed += () => {
                    if (string.IsNullOrEmpty(instanceScene.Asset?.AssetFilename)) {
                        GD.PrintErr("Asset filename field is missing");
                        return;
                    }

                    var importPath = instanceScene.Asset.GetImportFilepath(ReachForGodot.GetAssetConfig(instanceScene.Game));

                    var res = ResourceLoader.Exists(importPath) ? ResourceLoader.Load<PackedScene>(importPath) : new PackedScene();
                    res.Pack(instanceScene);
                    if (string.IsNullOrEmpty(res.ResourcePath)) {
                        res.ResourcePath = importPath;
                    } else {
                        res.TakeOverPath(importPath);
                    }
                    ResourceSaver.Save(res);
                    GD.Print("Updated scene resource: " + importPath);
                };
            } else {
                revertEditable.Visible = false;
            }
        }

        var importType = container.GetNode<OptionButton>("%ImportTypeOption");
        importType.Clear();
        if (obj is PrefabNode pfb) {
            importType.AddItem("Import anything missing", (int)GodotRszImporter.PresetImportModes.ImportTreeChanges);
            importType.AddItem("Discard and reimport structure", (int)GodotRszImporter.PresetImportModes.ReimportStructure);
            importType.AddItem("Fully reimport all resources", (int)GodotRszImporter.PresetImportModes.FullReimport);
            importType.Selected = lastSelectedPrefabImport;
        } else if (obj is SceneFolder scn) {
            importType.AddItem("Placeholders only", (int)GodotRszImporter.PresetImportModes.PlaceholderImport);
            importType.AddItem("Import just this scene, no subfolders", (int)GodotRszImporter.PresetImportModes.ThisFolderOnly);
            importType.AddItem("Import missing objects", (int)GodotRszImporter.PresetImportModes.ImportMissingItems);
            importType.AddItem("Discard and reimport scene structure", (int)GodotRszImporter.PresetImportModes.ReimportStructure);
            importType.AddItem("Force reimport all resources", (int)GodotRszImporter.PresetImportModes.FullReimport);
            importType.Selected = lastSelectedSceneImport;
        } else {
            importType.AddItem("Full import", (int)GodotRszImporter.PresetImportModes.ImportTreeChanges);
        }
        importType.ItemSelected += (index) => {
            if (obj is SceneFolder) lastSelectedSceneImport = (int)index;
            if (obj is PrefabNode) lastSelectedPrefabImport = (int)index;
        };

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
        if (obj.Asset?.IsEmpty != false) {
            importType.Visible = false;
            importBtn.Visible = false;
        }

        AddCustomControl(container);
        pluginSerializationFixer.Register((GodotObject)obj, container);

        // the flow container doesn't refresh its height properly, force it to do so
        var hflow = container.FindChildByTypeRecursive<HFlowContainer>();
        if (hflow != null) {
            hflow.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            Task.Delay(5).ContinueWith(_ => {
                hflow.SetDeferred(Control.PropertyName.SizeFlagsHorizontal, (int)Control.SizeFlags.Fill);
            });
        }
    }

    private async Task DoRebuild(IAssetPointer root, RszGodotConversionOptions options)
    {
        var sw = new Stopwatch();
        sw.Start();

        var conv = new GodotRszImporter(ReachForGodot.GetAssetConfig(root.Game), options);

        Task task;
        if (root is PrefabNode pfb) {
            task = conv.RegeneratePrefabTree(pfb);
        } else if (root is RcolRootNode rcol) {
            conv.GenerateRcol(rcol);
            task = Task.CompletedTask;
        } else if (root is SceneFolder scn) {
            if (scn.GetParent() is SceneFolderProxy parentProxy) {
                root = scn = parentProxy;
            }
            var sourceIsInstance = false;
            if (scn is SceneFolderProxy proxy) {
                var realScene = proxy.Contents;
                scn = realScene!.Instantiate<SceneFolder>();
                proxy.UnloadScene();
            } else if (scn.Owner != null) {
                if (string.IsNullOrEmpty(scn.Asset?.AssetFilename)) {
                    GD.PrintErr("Can't build editable scene with no source asset: " + scn.Path);
                    return;
                }

                var linkedScene = Importer.FindOrImportResource<PackedScene>(scn.Asset.AssetFilename, conv.AssetConfig);
                var newScn = linkedScene!.Instantiate<SceneFolder>();
                scn.GetParent().EmplaceChild(scn, newScn);
                scn = newScn;
            }
            task = conv.RegenerateSceneTree(scn);
            if (sourceIsInstance) {
                EditorInterface.Singleton.EditNode(scn);
            }
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