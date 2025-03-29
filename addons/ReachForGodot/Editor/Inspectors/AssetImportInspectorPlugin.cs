#if TOOLS
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;
using ReaGE.EditorLogic;

namespace ReaGE;

public partial class AssetImportInspectorPlugin : EditorInspectorPlugin, ISerializationListener
{
    private static PluginSerializationFixer pluginSerializationFixer = new();

    public void OnAfterDeserialize() { }
    public void OnBeforeSerialize() => pluginSerializationFixer.OnBeforeSerialize();

    private PackedScene? inspectorScene;

    private static Dictionary<Type, int> lastSelectedImportTypes = new();

    public override bool _CanHandle(GodotObject @object)
    {
        return @object is IImportableAsset and GodotObject;
    }

    public override void _ParseBegin(GodotObject @object)
    {
        if (@object is IImportableAsset importable) {
            CreateUI(importable);
        }
    }

    private void CreateUI(IImportableAsset importable)
    {
        inspectorScene ??= ResourceLoader.Load<PackedScene>("res://addons/ReachForGodot/Editor/Inspectors/AssetImportInspector.tscn");
        var container = inspectorScene.Instantiate<Control>();

        var mainImportedType = importable is SceneFolderProxy ? typeof(SceneFolder) : importable.GetType();

        var importType = container.GetNode<OptionButton>("%ImportTypeOption");
        var sourcesContainer = container.GetNode<Control>("%ImportSourceContainer");
        var importBtn = container.GetNode<Button>("%ImportButton");
        importType.Clear();
        if (importable.Asset?.IsEmpty == false) {
            foreach (var it in importable.SupportedImportTypes) {
                importType.AddItem(it.label, (int)it.importMode);
            }
            importType.Selected = lastSelectedImportTypes.GetValueOrDefault(mainImportedType);
            importType.Visible = true;

            Label? emptyLabel = null;
            if (importable.IsEmpty) {
                var importSection = container.GetNode<Container>("%ImportSection");
                emptyLabel = new Label() { Text = "Object is uninitialized. Make sure a source asset is defined and press 'Import'." };
                importSection.GetParent().AddChild(emptyLabel);
                importSection.GetParent().MoveChild(emptyLabel, importSection.GetIndex());
            }

            importType.ItemSelected += (index) => {
                lastSelectedImportTypes[mainImportedType] = (int)index;
            };

            var fileSources = PathUtils.FindFileSourceFolders(importable.Asset?.AssetFilename, ReachForGodot.GetAssetConfig(importable.Game)).ToArray();
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

            importBtn.Pressed += async () => {
                var source = sourceOption.Selected == -1 ? fileSources.FirstOrDefault() : fileSources[sourceOption.Selected];
                var config = ReachForGodot.GetAssetConfig(importable.Game);
                if (source == null) {
                    if (!FileUnpacker.TryExtractFile(importable.Asset!.AssetFilename, config)) {
                        GD.PrintErr("Could not determine file source path. Verify it is present in the chunk or additional folders");
                        return;
                    }
                    source = null;
                }

                config.Paths.SourcePathOverride = source;
                try {
                    var options = ((PresetImportModes)importType.GetSelectedId()).ToOptions();
                    await DoRebuild(importable, options);
                    if (emptyLabel != null) emptyLabel.Visible = importable.IsEmpty;
                } finally {
                    config.Paths.SourcePathOverride = null;
                }
            };
        } else {
            importType.Visible = false;
            sourcesContainer.Visible = false;
            importBtn.Visible = false;
        }

        AddCustomControl(container);
        pluginSerializationFixer.Register((GodotObject)importable, container);
    }

    private async Task DoRebuild(IImportableAsset root, GodotImportOptions options)
    {
        var sw = new Stopwatch();
        sw.Start();

        AssetConfig config = ReachForGodot.GetAssetConfig(root.Game);
        var conv = new GodotRszImporter(config, options);
        var sourceFilepath = PathUtils.FindSourceFilePath(root.Asset?.AssetFilename, config);
        if (sourceFilepath == null) {
            GD.PrintErr("Source file not found");
            return;
        }

        IImportableAsset asset = root;
        if (root is SceneFolder scn) {
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
            asset = scn;
            if (sourceIsInstance) {
                EditorInterface.Singleton.EditNode(scn);
            }
        }

        try {
            await asset.Import(sourceFilepath, conv);
            GD.Print("Resource reimport finished in " + sw.Elapsed);
            if (root is SceneFolderProxy proxy) {
                if (proxy.ShowLinkedFolder) {
                    proxy.LoadScene();
                }
            } else {
                EditorInterface.Singleton.CallDeferred(EditorInterface.MethodName.MarkSceneAsUnsaved);
            }
        } catch (Exception e) {
            GD.Print("Resource reimport failed:", e);
        }
    }
}
#endif