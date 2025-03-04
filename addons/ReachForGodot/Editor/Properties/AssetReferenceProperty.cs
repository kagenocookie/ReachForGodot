namespace RGE;

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

    private AssetReference GetAsset() => (GetEditedObject() as IRszContainerNode)?.Asset ?? throw new NullReferenceException("Edited object has no asset reference object");
    private string? CurrentResourcePath => GetEditedObject() is Resource res ? res.ResourcePath.GetBaseName() : GetEditedObject() is Node node && !string.IsNullOrEmpty(node.SceneFilePath) ? node.SceneFilePath.GetBaseName() : null;
    private SupportedGame Game => (GetEditedObject() as IRszContainerNode)?.Game ?? SupportedGame.Unknown;

    public override void _EnterTree()
    {
        inspectorScene ??= ResourceLoader.Load<PackedScene>("res://addons/ReachForGodot/Editor/Inspectors/AssetReferenceInspectorPlugin.tscn");
        container = inspectorScene.Instantiate<Control>();
        AddChild(container);
        SetupUI();
    }

    private void DoShow()
    {
        GetAsset().OpenSourceFile(Game);
    }

    private void DoUpdatePath()
    {
        var text = container.GetNode<LineEdit>("%Input");
        var updatePathBtn = container.GetNode<Button>("%UpdatePathBtn");
        text.Text = PathUtils.ImportPathToRelativePath(CurrentResourcePath!, ReachForGodot.GetAssetConfig(Game))!;
        this.SetPropertyUndoable(GetAsset(), AssetReference.PropertyName.AssetFilename, text.Text);
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

        this.SetPropertyUndoable(GetAsset(), AssetReference.PropertyName.AssetFilename, newText);
    }

    private void SetupUI()
    {
        var target = GetEditedObject();

        var text = container.GetNode<LineEdit>("%Input");
        var showBtn = container.GetNode<Button>("%ShowBtn");
        var updatePathBtn = container.GetNode<Button>("%UpdatePathBtn");

        lastAsset = GetAsset();

        text.Text = lastAsset!.AssetFilename ?? string.Empty;
        RefreshUI();

        lastAsset.Changed += ValueChanged;
        showBtn.Pressed += DoShow;
        updatePathBtn.Pressed += DoUpdatePath;
        text.TextChanged += DoTextChanged;
    }

    private void RefreshUI()
    {
        var updatePathBtn = container.GetNode<Button>("%UpdatePathBtn");
        var targetRsz = GetEditedObject() as IRszContainerNode;
        var currentResourcePath = CurrentResourcePath;
        var expectedImportPath = targetRsz?.Asset?.GetImportFilepath(ReachForGodot.GetAssetConfig(Game))?.GetBaseName();
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