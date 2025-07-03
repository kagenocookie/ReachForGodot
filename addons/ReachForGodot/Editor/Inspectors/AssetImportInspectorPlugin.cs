#if TOOLS
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;

namespace ReaGE;

public partial class AssetImportInspectorPlugin : EditorInspectorPlugin, ISerializationListener
{
    private static PluginSerializationFixer pluginSerializationFixer = new();

    public void OnAfterDeserialize() { }
    public void OnBeforeSerialize() => pluginSerializationFixer.OnBeforeSerialize();

    private PackedScene? inspectorScene;

    private static Dictionary<Type, GodotImportOptions> lastSelectedImportTypes = new();

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
        var config = ReachForGodot.GetAssetConfig(importable.Game);
        if (!config.IsValid) {
            AddCustomControl(new Label() { Text = $"{importable.Game} is not fully configured. Please define at least a chunk path in editor settings." });
            return;
        }

        inspectorScene ??= ResourceLoader.Load<PackedScene>("res://addons/ReachForGodot/Editor/Inspectors/AssetImportInspector.tscn");
        var container = inspectorScene.Instantiate<Control>();

        var mainImportedType = importable is SceneFolderProxy ? typeof(SceneFolder) : importable.GetType();

        var importType = container.GetNode<OptionButton>("%ImportTypeOption");
        var sourcesContainer = container.GetNode<Control>("%ImportSourceContainer");
        var importBtn = container.GetNode<Button>("%ImportButton");
        importType.Clear();
        if (importable.Asset?.IsEmpty == false) {
            var modes = importable.SupportedImportTypes.ToArray();
            var lastSelected = lastSelectedImportTypes.GetValueOrDefault(mainImportedType);
            foreach (var it in modes) {
                importType.AddItem(it.label);
                if (lastSelected == it.importMode) importType.Selected = importType.ItemCount - 1;
            }
            importType.Visible = true;

            Label? emptyLabel = null;
            if (importable.IsEmpty) {
                var importSection = container.GetNode<Container>("%ImportSection");
                emptyLabel = new Label() { Text = "Object is uninitialized. Make sure a source asset is defined and press 'Import'." };
                importSection.GetParent().AddChild(emptyLabel);
                importSection.GetParent().MoveChild(emptyLabel, importSection.GetIndex());
            }

            importType.ItemSelected += (index) => {
                lastSelectedImportTypes[mainImportedType] = modes[(int)index].importMode;
            };

            var fileSources = importable.Asset.AssetFilename.StartsWith("res://")
                ? [ new LabelledPathSetting(ProjectSettings.GlobalizePath(importable.Asset.AssetFilename)) ]
                : PathUtils.FindFileSourceFolders(importable.Asset.AssetFilename, ReachForGodot.GetAssetConfig(importable.Game)).ToArray();

            if (fileSources.Length == 0) {
                if (!FileUnpacker.TryExtractFile(importable.Asset.AssetFilename, config)) {
                    var importSection = container.GetNode<Container>("%ImportSection");
                    emptyLabel = new Label() { Text = "File could not be found in any source paths nor extracted from paks. It can't be re-imported." };
                    importSection.GetParent().AddChild(emptyLabel);
                    importSection.GetParent().MoveChild(emptyLabel, importSection.GetIndex());
                } else {
                    fileSources = PathUtils.FindFileSourceFolders(importable.Asset.AssetFilename, ReachForGodot.GetAssetConfig(importable.Game)).ToArray();
                }
            }

            var sourceOption = sourcesContainer.RequireChildByType<OptionButton>();
            var openSourceBtn = container.GetNode<Button>("%ShowImportSourceBtn");
            if (fileSources.Length > 1) {
                sourcesContainer.Visible = true;
                sourceOption.Clear();
                foreach (var src in fileSources) {
                    sourceOption.AddItem(src.label);
                }
                openSourceBtn.Pressed += () => {
                    if (Path.IsPathRooted(fileSources[sourceOption.Selected]) && File.Exists(fileSources[sourceOption.Selected])) {
                        FileSystemUtils.ShowFileInExplorer(fileSources[sourceOption.Selected]);
                    } else {
                        FileSystemUtils.ShowFileInExplorer(PathUtils.FindSourceFilePath(Path.Combine(fileSources[sourceOption.Selected], importable.Asset!.AssetFilename), config));
                    }
                };
            } else {
                sourcesContainer.Visible = false;
            }

            importBtn.Pressed += async () => {
                var source = sourceOption.Selected == -1 ? fileSources.FirstOrDefault() : fileSources[sourceOption.Selected];
                var config = ReachForGodot.GetAssetConfig(importable.Game);
                if (source == null) {
                    GD.PrintErr("Could not determine file source path. Verify it is present in the chunk or additional folders");
                    return;
                }
                var isFullPath = Path.IsPathRooted(source) && File.Exists(source);

                if (!isFullPath) {
                    config.Paths.SourcePathOverride = PathUtils.GetSourceFileBasePath(source!, config);
                }
                try {
                    var options = modes[importType.GetSelectedId()].importMode;
                    await DoRebuild(importable, options, isFullPath ? source : null);
                    if (emptyLabel != null && IsInstanceValid(emptyLabel)) emptyLabel.Visible = importable.IsEmpty;
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

    private async Task DoRebuild(IImportableAsset root, GodotImportOptions options, string? sourceFilepath)
    {
        var sw = new Stopwatch();
        sw.Start();

        var config = ReachForGodot.GetAssetConfig(root.Game);
        var converter = new AssetConverter(config, options);
        sourceFilepath ??= PathUtils.FindSourceFilePath(root.Asset?.AssetFilename, config);
        if (sourceFilepath == null) {
            GD.PrintErr("Source file not found: " + root.Asset?.AssetFilename);
            return;
        }
        var importFilepath = PathUtils.GetAssetImportPath(sourceFilepath, config);
        var isRoot = root == EditorInterface.Singleton.GetEditedSceneRoot();

        IImportableAsset asset = root;
        if (root is SceneFolder scn) {
            if (scn.GetParent() is SceneFolderProxy parentProxy) {
                root = scn = parentProxy;
            }
            if (scn is SceneFolderProxy proxy) {
                var realScene = proxy.Contents;
                scn = realScene!.Instantiate<SceneFolder>();
                proxy.UnloadScene();
            } else if (scn.Owner != null) {
                if (string.IsNullOrEmpty(scn.Asset?.AssetFilename)) {
                    GD.PrintErr("Can't build editable scene with no source asset: " + scn.Path);
                    return;
                }

                var linkedScene = Importer.FindOrImportAsset<PackedScene>(scn.Asset.AssetFilename, config);
                var newScn = linkedScene!.Instantiate<SceneFolder>();
                scn.GetParent().EmplaceChild(scn, newScn);
                scn = newScn;
            }
            asset = scn;
        }

        try {
            await converter.ImportAssetAsync(asset, sourceFilepath);
            if (asset is Node node) {
                EditorInterface.Singleton.EditNode(node);
                if (!isRoot && node is SceneFolder or PrefabNode && !string.IsNullOrEmpty(importFilepath)) {
                    node.SaveAsScene(importFilepath);
                }
            }
            GD.Print("Resource reimport finished in " + sw.Elapsed);
            if (root is SceneFolderProxy proxy) {
                if (proxy.ShowLinkedFolder) {
                    proxy.LoadScene();
                }
            } else if (asset is REResourceProxy outProxy) {
                if (outProxy.ImportedResource != null) {
                    if (outProxy.ImportedResource is PackedScene) {
                        EditorInterface.Singleton.CallDeferred(EditorInterface.MethodName.OpenSceneFromPath, outProxy.ImportedResource.ResourcePath);
                    } else {
                        EditorInterface.Singleton.CallDeferred(EditorInterface.MethodName.EditResource, outProxy.ImportedResource);
                    }
                } else {
                    EditorInterface.Singleton.CallDeferred(EditorInterface.MethodName.SelectFile, outProxy.ResourcePath);
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