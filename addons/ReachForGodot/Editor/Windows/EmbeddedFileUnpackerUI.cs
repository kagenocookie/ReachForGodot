using CustomFileBrowser;
using Godot;
using ReeLib;

namespace ReaGE;

[GlobalClass, Tool]
public partial class EmbeddedFileUnpackerUI : Control
{
    [Export] public FileDialog.FileModeEnum FileMode { get; set; }
    [Export] public FilePickerPanel? FilePanel { get; set; }
    [Export] public Container? ExtensionButtonsContainer { get; set; }
    [Export] private OptionButton? GamePicker { get; set; }
    public bool ShouldImportFiles { get; private set; }
    public bool ForceExtractFiles { get; private set; }
    public FileListFileSystem? FileSystem { get; set; }

    private SupportedGame _game;
    public SupportedGame Game {
        get => _game;
        set {
            if (_game == value) return;
            _game = value;
            if (GamePicker != null) GamePicker.Selected = GamePicker.GetItemIndex((int)value);
            ReloadFileLists();
        }
    }

    [Signal] public delegate void FilesSelectedEventHandler(string[] files);
    public List<string> SelectedExtensions { get; } = new();

    public string? CurrentDir { get; set; }
    public string? CurrentFile { get; set; }

    private string[] CurrentFilesList { get; set; } = Array.Empty<string>();

    public override void _EnterTree()
    {
        base._EnterTree();
        if (FilePanel == null) {
            FilePanel = new FilePickerPanel(FileSystem!);
            AddChild(FilePanel);
            FilePanel.FileSelected += OnFileSelected;
            FilePanel.FilesSelected += OnFilesSelected;
        }
        if (GamePicker != null) {
            GamePicker.Clear();
            foreach (var game in ReachForGodot.GameList) {
                if (ReachForGodot.GetAssetConfig(game).IsValid) {
                    GamePicker.AddItem(game.ToString(), (int)game);
                }
            }
            GamePicker.Selected = GamePicker.GetItemIndex((int)_game);
        }

        if (_game == SupportedGame.Unknown) {
            ExtensionButtonsContainer?.QueueFreeRemoveChildrenWhere(c => c is CheckButton);
        }

        FilePanel.FileMode = this.FileMode;
        FilePanel.FileSystem = FileSystem ?? FilePanel.FileSystem;
    }

    private void OnSelectedGameChanged(int index)
    {
        var game = (SupportedGame)(GamePicker!.GetItemId(index));
        Game = game;
    }

    private static readonly KnownFileFormats[] PinnedFileFormats = [
        KnownFileFormats.Mesh,
        KnownFileFormats.MaterialDefinition,
        KnownFileFormats.Scene,
        KnownFileFormats.Prefab,
        KnownFileFormats.UserData,
        KnownFileFormats.RequestSetCollider,
        KnownFileFormats.Texture,
        KnownFileFormats.Message,
        KnownFileFormats.CollisionMesh,
        KnownFileFormats.Effect,
        KnownFileFormats.UserVariables,
    ];

    private void ReloadFileLists()
    {
        var config = ReachForGodot.GetAssetConfig(Game);
        if (!config.IsValid) return;

        ExtensionButtonsContainer?.QueueFreeRemoveChildrenWhere(c => c is CheckButton);

        if (!config.Workspace.CanUseListFile) {
            FileSystem = new FileListFileSystem(Array.Empty<string>());
            return;
        }

        FileSystem = new FileListFileSystem(config.Workspace.ListFile!.Files);
        if (ExtensionButtonsContainer != null) {
            var orderedFormats = config.Workspace.GameFileExtensions
                .Select(ext => (ext, fmt: PathUtils.GetFileFormatFromExtension(ext)))
                .OrderBy(x => Array.IndexOf(PinnedFileFormats, x.fmt) is int pinned && x.fmt.IsSupportedFileFormat() ? (pinned == -1 ? 100 : pinned) : 200)
                // .OrderBy(x => !x.fmt.IsSupportedFileFormat() ? 200 : Array.IndexOf(PinnedFileFormats, x.fmt))
                .ThenBy(x => x.ext)
                .Select(x => x.ext)
                .ToList();

            foreach (var ext in orderedFormats) {
                var btn = new CheckButton() { Text = ext };
                btn.Toggled += (toggled) => {
                    if (toggled) SelectedExtensions.Add(ext);
                    else SelectedExtensions.Remove(ext);
                    SelectedExtensionsChanged();
                };
                ExtensionButtonsContainer.AddChild(btn);
            }
        }
        if (FilePanel != null) {
            FilePanel.FileSystem = FileSystem;
            if (string.IsNullOrEmpty(FilePanel.CurrentDir)) {
                FilePanel.RefreshItems();
            } else {
                FilePanel.CurrentDir = string.Empty;
            }
        }
    }

    private void OnFilesSelected(string[] files)
    {
        ImportOnly();
    }

    private void OnFileSelected(string file)
    {
        ImportOnly();
    }

    private void SelectedExtensionsChanged()
    {
        if (FileSystem == null) return;

        FileSystem.Filter = SelectedExtensions.Count == 0 ? null : FilterFileByExtension;
        FilePanel!.DisplayMode = SelectedExtensions.Count == 0 ? FilePickerPanel.FileDisplayMode.SingleFolder : FilePickerPanel.FileDisplayMode.Recursive;
        FilePanel?.RefreshItems();
    }

    private bool FilterFileByExtension(ReadOnlySpan<char> path)
    {
        var pathExt = PathUtils.GetFilenameExtensionWithSuffixes(path);
        int dot = -1;
        if (pathExt.IsEmpty || (dot = pathExt.IndexOf('.')) == -1) return false;
        pathExt = pathExt[0..dot];
        foreach (var ext in SelectedExtensions) {
            if (pathExt.SequenceEqual(ext)) {
                return true;
            }
        }

        return false;
    }

    private void ExtractOnly()
    {
        ShouldImportFiles = false;
        ForceExtractFiles = true;
        ConfirmSelectedFile();
    }

    private void ImportOnly()
    {
        ShouldImportFiles = true;
        ForceExtractFiles = false;
        ConfirmSelectedFile();
    }

    private void ExtractImport()
    {
        ShouldImportFiles = true;
        ForceExtractFiles = false;
        ConfirmSelectedFile();
    }

    private void ExtractFilteredFiles()
    {
        if (FileMode == FileDialog.FileModeEnum.OpenFiles) {
            var files = FileSystem?.GetRecursiveFileList(CurrentDir ?? string.Empty).ToArray();
            if (files != null) EmitSignal(SignalName.FilesSelected, files);
        } else if (FileMode == FileDialog.FileModeEnum.SaveFile) {
            EmitSignal(SignalName.FilesSelected, [FilePanel!.LastSelectedItem!]);
        } else {
            EmitSignal(SignalName.FilesSelected, [FilePanel!.LastSelectedItem!]);
        }
    }

    private void ConfirmSelectedFile()
    {
        var selected = FilePanel?.SelectedFilepaths;
        if (selected == null || !selected.Any()) return;

        if (FileMode == FileDialog.FileModeEnum.OpenFiles) {
            EmitSignal(SignalName.FilesSelected, selected.ToArray());
        } else if (FileMode == FileDialog.FileModeEnum.SaveFile) {
            EmitSignal(SignalName.FilesSelected, [FilePanel!.LastSelectedItem!]);
        } else {
            EmitSignal(SignalName.FilesSelected, [FilePanel!.LastSelectedItem!]);
        }
    }

    private void SelectAllExtensions()
    {
        SelectedExtensions.Clear();
        SelectedExtensions.AddRange(ReachForGodot.GetAssetConfig(Game).Workspace.GameFileExtensions);
        RefreshExtensionButtons();
    }

    private void DeselectAllExtensions()
    {
        SelectedExtensions.Clear();
        RefreshExtensionButtons();
    }

    private void RefreshExtensionButtons()
    {
        if (ExtensionButtonsContainer == null) return;

        foreach (var btn in ExtensionButtonsContainer.FindChildrenByType<CheckButton>()) {
            btn.SetPressedNoSignal(SelectedExtensions.Contains(btn.Text));
        }
        SelectedExtensionsChanged();
    }

    public static string NormalizeFilepath(string path)
    {
        if (path.IndexOf('\\') != -1) return path.Replace('\\', '/');
        return path;
    }
}
