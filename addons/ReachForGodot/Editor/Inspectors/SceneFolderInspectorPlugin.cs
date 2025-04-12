#if TOOLS
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
                    instanceScene.SetRecursiveOwner(instanceScene, instanceScene.Owner);
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
}
#endif