using Godot;

namespace ReaGE;

[GlobalClass, Tool]
public partial class MainWindow : Control
{
    [Export] private ImportPanel filedropPanel = null!;
    [Export] private VBoxContainer savedFileListContainer = null!;

    public override void _EnterTree()
    {
        // GD.Print("RFG window init");
        // EditorInterface.Singleton.GetEditorViewport3D().
        // (GetViewport() as SubViewport).
        filedropPanel.ObjectDragged += OnFileDropped;
        GetWindow().FilesDropped += OnFilesDropped;
        // EditorInterface.Singleton.GetInspector().EditedObjectChanged
    }

    private void OnFileDropped(GodotObject file)
    {
        // todo check if we already have the object
        savedFileListContainer.AddChild(new Button() {
            Text = (file as Node)?.Name ?? (file as Resource)?.ResourceName ?? file.ToString()
        });
    }

    public override void _ExitTree()
    {
        GD.Print("RFG window _ExitTree");
        GetWindow().FilesDropped -= OnFilesDropped;
    }

    private void OnFilesDropped(string[] files)
    {
        GD.Print("Self is visible: " + this.Visible + " / " + IsVisibleInTree());
        GD.Print("Dropped:\n" + string.Join("\n", files));
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return true;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        GD.Print($"Dropped: {atPosition} data: {data}");
    }
}
