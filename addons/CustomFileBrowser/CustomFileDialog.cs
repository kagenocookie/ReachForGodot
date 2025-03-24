using Godot;

namespace CustomFileBrowser;

[GlobalClass, Tool]
public partial class CustomFileDialog : ConfirmationDialog
{
    public CustomFileDialog()
    {
    }

    public CustomFileDialog(ICustomFileSystem filesystem)
    {
        this.FileSystem = filesystem;
    }

    [Export] public FileDialog.FileModeEnum FileMode { get; set; }
    [Export] public FilePickerPanel? FilePanel { get; set; }
    public ICustomFileSystem? FileSystem { get; set; }

    [Signal] public delegate void FileSelectedEventHandler(string file);
    [Signal] public delegate void FilesSelectedEventHandler(string[] files);

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
        FilePanel.FileMode = this.FileMode;
        FilePanel.FileSystem = FileSystem ?? FilePanel.FileSystem;
    }

    private void OnFilesSelected(string[] files)
    {
        GetOkButton().EmitSignal(Button.SignalName.Pressed);
    }

    private void OnFileSelected(string file)
    {
        GetOkButton().EmitSignal(Button.SignalName.Pressed);
    }

    protected virtual void OnConfirm()
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
    }

    public static string NormalizeFilepath(string path)
    {
        if (path.IndexOf('\\') != -1) return path.Replace('\\', '/');
        return path;
    }
}
