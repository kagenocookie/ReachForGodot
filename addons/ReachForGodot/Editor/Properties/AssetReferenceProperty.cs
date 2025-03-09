namespace ReaGE;

using System;
using System.Text.RegularExpressions;
using Godot;

[Tool]
public partial class AssetReferenceProperty : EditorProperty
{
    private LineEdit edit = null!;
    private Control container = null!;

    private PackedScene? inspectorScene;
    private AssetReference? lastAsset;

    [GeneratedRegex("[^a-zA-Z0-9-_/\\.]")]
    private static partial Regex FilepathRegex();

    private AssetReference? GetAsset() => GetEditedObject().Get(GetEditedProperty()).As<AssetReference>();
    private string? CurrentResourcePath => GetEditedObject() is Resource res ? res.ResourcePath.GetBaseName() : GetEditedObject() is Node node && !string.IsNullOrEmpty(node.SceneFilePath) ? node.SceneFilePath.GetBaseName() : null;
    private SupportedGame Game => (GetEditedObject() as IRszContainerNode)?.Game ?? (GetEditedObject() as REResource)?.Game ?? SupportedGame.Unknown;

    public override void _EnterTree()
    {
        inspectorScene ??= ResourceLoader.Load<PackedScene>("res://addons/ReachForGodot/Editor/Inspectors/AssetReferenceInspectorPlugin.tscn");
        container = inspectorScene.Instantiate<Control>();
        AddChild(container);
        SetupUI();
    }

    private void DoShow()
    {
        GetAsset()?.OpenSourceFile(Game);
    }

    private void DoUpdatePath()
    {
        var text = container.GetNode<LineEdit>("%Input");
        var updatePathBtn = container.GetNode<Button>("%UpdatePathBtn");
        text.Text = PathUtils.ImportPathToRelativePath(CurrentResourcePath!, ReachForGodot.GetAssetConfig(Game))!;
        UpdatePath(text.Text);
        updatePathBtn.Visible = false;
    }

    private void DoTextChanged(string newText)
    {
        var text = container.GetNode<LineEdit>("%Input");
        var updatePathBtn = container.GetNode<Button>("%UpdatePathBtn");
        var fixedText = FilepathRegex().Replace(newText, "");
        if (fixedText != text.Text) {
            text.Text = newText = fixedText;
        }

        UpdatePath(newText);
    }

    private void UpdatePath(string newPath)
    {
        var asset = GetAsset();
        if (asset == null) {
            asset = new AssetReference(newPath);
            lastAsset = asset;
            asset.Changed += ValueChanged;
            this.SetPropertyUndoable(GetEditedObject(), GetEditedProperty(), asset);
        } else {
            this.SetPropertyUndoable(asset, AssetReference.PropertyName.AssetFilename, newPath);
        }
    }

    private void SetupUI()
    {
        var target = GetEditedObject();

        var text = container.GetNode<LineEdit>("%Input");
        var showBtn = container.GetNode<Button>("%ShowBtn");
        var updatePathBtn = container.GetNode<Button>("%UpdatePathBtn");

        lastAsset = GetAsset();

        text.Text = lastAsset?.AssetFilename ?? string.Empty;
        RefreshUI();

        if (lastAsset != null) {
            lastAsset.Changed += ValueChanged;
        }
        showBtn.Pressed += DoShow;
        updatePathBtn.Pressed += DoUpdatePath;
        text.TextChanged += DoTextChanged;
    }

    private void RefreshUI()
    {
        var updatePathBtn = container.GetNode<Button>("%UpdatePathBtn");
        var targetAsset = (GetEditedObject() as IRszContainerNode)?.Asset ?? (GetEditedObject() as REResource)?.Asset;
        var currentResourcePath = CurrentResourcePath;
        var expectedImportPath = targetAsset?.GetImportFilepath(ReachForGodot.GetAssetConfig(Game))?.GetBaseName();
        updatePathBtn.Visible = !string.IsNullOrEmpty(currentResourcePath) && !currentResourcePath.Equals(expectedImportPath, StringComparison.OrdinalIgnoreCase);
    }

    private void ValueChanged()
    {
        var text = GetAsset()?.AssetFilename;
        var input = container.GetNode<LineEdit>("%Input");
        if (input.Text != text) {
            input.Text = text;
        }
        RefreshUI();
    }
}