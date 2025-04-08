using CustomFileBrowser;
using Godot;

namespace ReaGE;

[GlobalClass, Tool]
public partial class FileUnpackerUI : Window
{
    public FileUnpackerUI()
    {
    }

    public FileUnpackerUI(FileListFileSystem filesystem)
    {
        this.FileSystem = filesystem;
    }

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

    [Signal] public delegate void FileSelectedEventHandler(string file);
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

        FilePanel.FileMode = this.FileMode;
        FilePanel.FileSystem = FileSystem ?? FilePanel.FileSystem;
    }

    private void OnSelectedGameChanged(int index)
    {
        var game = (SupportedGame)(GamePicker!.GetItemId(index));
        Game = game;
    }

    private void ReloadFileLists()
    {
        var config = ReachForGodot.GetAssetConfig(Game);
        if (!config.IsValid) return;
        ExtensionButtonsContainer?.ClearChildren(c => c is CheckButton);
        if (string.IsNullOrEmpty(config.Paths.FilelistPath)) {
            FileSystem = new FileListFileSystem(Array.Empty<string>());
            return;
        }

        FileSystem = new FileListFileSystem(config.Paths.FilelistPath);
        if (ExtensionButtonsContainer != null) {
            var orderedFormats = PathUtils.GetGameFileExtensions(Game)
                .OrderBy(ext => PathUtils.GetFileFormatFromExtension(ext) is SupportedFileFormats fmt
                    && fmt != SupportedFileFormats.Unknown ? ((int)fmt, string.Empty) : (99, ext));

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
        Hide();
    }

    private void OnFileSelected(string file)
    {
        ImportOnly();
        Hide();
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

    private void ExtractFilteredFiles()
    {
        if (FileMode == FileDialog.FileModeEnum.OpenFiles) {
            var files = FileSystem?.GetRecursiveFileList(CurrentDir ?? string.Empty).ToArray();
            if (files != null) EmitSignal(SignalName.FilesSelected, files);
        } else if (FileMode == FileDialog.FileModeEnum.SaveFile) {
            EmitSignal(SignalName.FileSelected, FilePanel!.LastSelectedItem!);
        } else {
            EmitSignal(SignalName.FileSelected, FilePanel!.LastSelectedItem!);
        }
        Hide();
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

    private void ConfirmSelectedFile()
    {
        var selected = FilePanel?.SelectedFilepaths;
        if (selected == null || !selected.Any()) return;

        if (FileMode == FileDialog.FileModeEnum.OpenFiles) {
            EmitSignal(SignalName.FilesSelected, selected.ToArray());
        } else if (FileMode == FileDialog.FileModeEnum.SaveFile) {
            EmitSignal(SignalName.FileSelected, FilePanel!.LastSelectedItem!);
        } else {
            EmitSignal(SignalName.FileSelected, FilePanel!.LastSelectedItem!);
        }
        Hide();
    }

    private void OnCloseRequested()
    {
        Hide();
    }

    private void SelectAllExtensions()
    {
        SelectedExtensions.Clear();
        SelectedExtensions.AddRange(PathUtils.GetGameFileExtensions(Game));
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
