using Godot;

namespace CustomFileBrowser;

[GlobalClass, Tool]
public partial class FilePickerColumnSlider : ColorRect
{
    private bool isPressed = false;
    private bool isHovered = false;
    public string Path { get; set; } = string.Empty;

    public StyleBox? SelectedStyleBox { get; internal set; }

    private Control? ResizedControl => GetParent().GetChild(GetIndex() - 1) as Control;

    public float Width => ResizedControl?.CustomMinimumSize.X ?? 0;

    [Signal]
    public delegate void ResizedEventHandler(int width);

    public override void _Ready()
    {
        MouseEntered += OnItemMouseEntered;
        MouseExited += OnItemMouseExited;
    }

    private void OnItemMouseEntered()
    {
        isHovered = true;
    }

    private void OnItemMouseExited()
    {
        isHovered = false;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouse) {
            if (mouse.ButtonIndex == MouseButton.Left) {
                if (mouse.Pressed) {
                    isPressed = true;
                } else {
                    isPressed = false;
                }
            }
        } else if (@event is InputEventMouseMotion motion) {
            if (isPressed && ResizedControl != null) {
                ResizedControl.CustomMinimumSize = ResizedControl.CustomMinimumSize + new Vector2(motion.Relative.X, 0);
            }
        }
    }
}