using Godot;

namespace CustomFileBrowser;

[GlobalClass, Tool]
public partial class FilePickerItem : HBoxContainer
{
    private bool isPressed = false;
    private bool isHovered = false;
    private DateTime lastPressTime;
    private string _path = string.Empty;
    public string Path
    {
        get => _path;
        set {
            lastPressTime = default;
            _path = value;
        }
    }
    private bool _isSelected;
    public bool IsSelected {
        get => _isSelected;
        set {
            if (value != _isSelected) {
                _isSelected = value;
                UpdateStyle();
                EmitSignal(SignalName.SelectedChanged);
            }
        }
    }

    public StyleBox? DefaultStyleBox { get; internal set; }
    public StyleBox? SelectedStyleBox { get; internal set; }
    public StyleBox? HoveredStyleBox { get; internal set; }

    private Control HighlightNode => GetParent<Control>();

    [Signal] public delegate void PressedEventHandler();
    [Signal] public delegate void DoublePressedEventHandler();
    [Signal] public delegate void SelectedChangedEventHandler();

    public override void _Ready()
    {
        MouseEntered += OnItemMouseEntered;
        MouseExited += OnItemMouseExited;
        UpdateStyle();
    }

    private void OnItemMouseEntered()
    {
        isHovered = true;
        UpdateStyle();
    }

    private void OnItemMouseExited()
    {
        isHovered = false;
        UpdateStyle();
    }

    private void UpdateStyle()
    {
        if (IsSelected) {
            HighlightNode.AddThemeStyleboxOverride("panel", SelectedStyleBox);
        } else if (isHovered) {
            HighlightNode.AddThemeStyleboxOverride("panel", HoveredStyleBox);
        } else if (DefaultStyleBox != null) {
            HighlightNode.AddThemeStyleboxOverride("panel", DefaultStyleBox);
        } else {
            HighlightNode.RemoveThemeStyleboxOverride("panel");
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouse) {
            if (mouse.ButtonIndex == MouseButton.Left) {
                if (mouse.Pressed) {
                    isPressed = true;
                } else {
                    if (isPressed && isHovered) {
                        if (DateTime.Now - lastPressTime < TimeSpan.FromSeconds(0.5f)) {
                            lastPressTime = DateTime.Now;
                            EmitSignal(SignalName.DoublePressed);
                        } else {
                            lastPressTime = DateTime.Now;
                            IsSelected = true;
                            EmitSignal(SignalName.Pressed);
                        }
                    }
                    isPressed = false;
                }
            }
        }
    }
}