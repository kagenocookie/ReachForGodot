using Godot;
using ReaGE;

namespace CustomFileBrowser;

[GlobalClass, Tool]
public partial class FilePickerPanel : Control
{
    public ICustomFileSystem FileSystem { get; set; } = null!;

    [Export] private HBoxContainer? ColumnHeader { get; set; }
    [Export] private Container? ItemContainer { get; set; }
    [Export] private LineEdit? PathEdit { get; set; }

    [Export] private StyleBox? DefaultStyleBox { get; set; }
    [Export] private StyleBox? HoveredStyleBox { get; set; }
    [Export] private StyleBox? SelectedStyleBox { get; set; }

    [Export] public FileDialog.FileModeEnum FileMode { get; set; }

    [Signal] public delegate void FileSelectedEventHandler(string file);
    [Signal] public delegate void FilesSelectedEventHandler(string[] files);
    [Signal] public delegate void SelectedFileChangedEventHandler(string? file);

    private const int RecursiveMatchLimit = 1000;

    public FileDisplayMode DisplayMode { get; set; }

    public enum FileDisplayMode
    {
        SingleFolder,
        Recursive,
    }

    private readonly List<FilePickerItem> _selectedItems = new();
    public IReadOnlyList<FilePickerItem> SelectedItems => _selectedItems;
    public string? LastSelectedItem { get; private set; }
    public IEnumerable<string> SelectedFilepaths => _selectedItems.Select(s => s.Path);

    private readonly List<ColumnInfo> columns = new();
    private readonly List<string> items = new();

    private string? _currentDir;
    public string? CurrentDir {
        get => _currentDir;
        set => ChangeDir(value);
    }
    private string? _currentFile;
    public string? CurrentFile
    {
        get => _currentFile;
        set => EmitSignal(SignalName.SelectedFileChanged, (_currentFile = value) ?? new Variant());
    }

    private sealed record ColumnInfo(Control ctrl, string field, float width);

    public FilePickerPanel(ICustomFileSystem fileSystem)
    {
        FileSystem = fileSystem;
    }

    public FilePickerPanel()
    {
        FileSystem = new FileListFileSystem(Array.Empty<string>());
    }

    private void OnColumnAdded(Node node)
    {
        OnColumnsChanged();
    }

    private void OnColumnRemoved(Node node)
    {
        OnColumnsChanged();
    }

    private void OnColumnsChanged()
    {
        var prevcols = columns.ToArray();
        foreach (var col in columns) {
            col.ctrl.MinimumSizeChanged -= OnColumnsChanged;
        }
        columns.Clear();
        if (ColumnHeader == null) return;
        foreach (var child in ColumnHeader.GetChildren().OfType<Control>()) {
            if (child is Label) {
                child.MinimumSizeChanged += OnColumnsChanged;
                columns.Add(new ColumnInfo(child, child.Name, child.CustomMinimumSize.X));
            } else if (child is ColorRect) {
                // child.press
            }
        }
        UpdateList();
    }

    private void UpdateList()
    {
        if (ItemContainer == null || !IsInstanceValid(ItemContainer)) return;

        int itemIndex = 0;
        var selectedIndex = -1;
        foreach (var item in items) {
            if (item == CurrentFile) {
                selectedIndex = itemIndex;
            }

            var child = ItemContainer.GetChildOrNull<PanelContainer>(itemIndex++);
            FilePickerItem pickerItem;
            if (child == null) {
                ItemContainer.AddChild(child = new PanelContainer());
                child.AddChild(pickerItem = new FilePickerItem() {
                    SelectedStyleBox = SelectedStyleBox,
                    HoveredStyleBox = HoveredStyleBox,
                    DefaultStyleBox = DefaultStyleBox,
                });
                // child.Owner = Owner ?? this;
                // pickerItem.Owner = Owner ?? this;
                pickerItem.Pressed += () => OnItemPressed(pickerItem);
                pickerItem.DoublePressed += () => OnItemDoublePressed(pickerItem);
            } else {
                pickerItem = child.GetChild<FilePickerItem>(0);
            }
            pickerItem.Path = item;
            UpdateListItem(item, pickerItem, item == CurrentFile);
        }
        if (selectedIndex == -1) {
            CurrentFile = null;
        }

        RemoveAfterIndex(ItemContainer, itemIndex);
    }

    private void OnItemDoublePressed(FilePickerItem item)
    {
        var type = FileSystem.GetPathType(item.Path);
        if (type == PathType.File) {
            if (FileMode is FileDialog.FileModeEnum.OpenFile or FileDialog.FileModeEnum.SaveFile or FileDialog.FileModeEnum.OpenAny) {
                EmitSignal(SignalName.FileSelected, item.Path);
            } else if (FileMode == FileDialog.FileModeEnum.OpenFiles) {
                EmitSignal(SignalName.FilesSelected, [item.Path]);
            }
        } else {
            CurrentDir = item.Path;
        }
    }

    private void ClearSelection()
    {
        foreach (var sel in _selectedItems) {
            sel.IsSelected = false;
        }
        _selectedItems.Clear();
    }

    private void OnItemPressed(FilePickerItem item)
    {
        var path = item.Path;
        if (string.IsNullOrEmpty(path)) {
            CurrentDir = string.Empty;
            return;
        }

        if (FileMode == FileDialog.FileModeEnum.OpenFile) {
            ClearSelection();
        }

        if (FileMode == FileDialog.FileModeEnum.OpenFiles) {
            item.IsSelected = !item.IsSelected;
            if (item.IsSelected) _selectedItems.Add(item);
            else _selectedItems.Remove(item);
        } else {
            ClearSelection();
            item.IsSelected = true;
            _selectedItems.Add(item);
        }
        LastSelectedItem = item.Path;
        var type = FileSystem.GetPathType(path);
        if (type == PathType.Folder) {
            CurrentFile = path;
        } else if (FileMode is FileDialog.FileModeEnum.OpenFile or FileDialog.FileModeEnum.OpenFiles or FileDialog.FileModeEnum.SaveFile) {
            CurrentFile = path;
        }
    }

    private void MoveToParentFolder()
    {
        if (string.IsNullOrEmpty(CurrentDir)) return;
        var up = CustomFileDialog.NormalizeFilepath(Path.GetDirectoryName(CurrentDir) ?? string.Empty);
        CurrentDir = up;
    }

    private void UpdateListItem(string item, FilePickerItem container, bool selected)
    {
        int i = 0;
        foreach (var col in columns) {
            var value = FileSystem.GetPathInfo(item, col.field);
            var child = container.GetChildOrNull<Control>(i++);
            if (child == null) {
                container.AddChild(child = CreateControl(col.field));
                child.SetRecursiveOwner(Owner ?? this);
            }
            if (selected) {
                value += " (S)";
            }
            child.CustomMinimumSize = new Vector2(col.width, 0);
            UpdateControl(col.field, value, child);
        }
        RemoveAfterIndex(container, i);
    }

    protected virtual Control CreateControl(string field)
    {
        var label = new Label() { ClipContents = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimChar };
        return label;
    }

    protected virtual void UpdateControl(string field, string? value, Control control)
    {
        var label = control as Label ?? control.FindChildByTypeRecursive<Label>();
        if (label != null) {
            label.Text = value ?? "/";
        }
    }

    public override void _EnterTree()
    {
        if (ItemContainer != null && IsInstanceValid(ItemContainer)) {
            while (ItemContainer.GetChildOrNull<Node>(0) is Node n) {
                ItemContainer.RemoveChild(n);
                n.QueueFree();
            }
        }
        if (CurrentDir == null) {
            ChangeDir(string.Empty);
        }
    }

    private void ChangeDir(string? dir)
    {
        if (dir == null) dir = string.Empty;
        if (dir == _currentDir) return;
        _currentDir = dir;
        if (PathEdit != null) {
            PathEdit.Text = dir;
        }

        RefreshItems();
    }

    public void RefreshItems()
    {
        _currentDir ??= string.Empty;

        var files = DisplayMode == FileDisplayMode.SingleFolder
            ? FileSystem.GetFilesInFolder(_currentDir)
            : FileSystem.GetRecursiveFileList(_currentDir, RecursiveMatchLimit);
        ClearSelection();
        LastSelectedItem = null;
        CurrentFile = null;
        items.Clear();
        items.AddRange(files);
        UpdateList();
    }

    private void SetFile(string? filepath)
    {
        if (filepath == _currentFile) {
            return;
        }

        _currentFile = filepath;
        if (!string.IsNullOrEmpty(filepath)) {
            var dir = CustomFileDialog.NormalizeFilepath(Path.GetDirectoryName(filepath) ?? string.Empty);

            if (dir == _currentDir) return;
            ChangeDir(dir);
        }
    }

    private static void RemoveAfterIndex(Control ctrl, int index)
    {
        var fullcount = ctrl.GetChildCount();
        var extras = fullcount - index;
        for (int r = 1; r <= extras; ++r) {
            var child = ctrl.GetChild(fullcount - r);
            ctrl.RemoveChild(child);
            child.QueueFree();
        }
    }
}
