namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class SearchResultItem : PanelContainer
{
    private bool isPressed = false;
    private bool isHovered = false;

    private StyleBox? defaultStyleBox;
    [Export] private StyleBox? hoverStyleBox;
    [Export] private StyleBox? pressedStyleBox;
    [Export] private RichTextLabel label = null!;
    [Export] private RichTextLabel context = null!;
    [Export] private TextureRect? icon;

    private void UpdateItemPreview(GodotObject? target)
    {
        if (icon == null) return;

        // ideally we'd just use EditorResourcePreview but it's not accessible outside of c++ ¯\_(ツ)_/¯
        icon.Visible = target != null;
        if (target is PrefabNode) {
            icon.Texture = ResourceLoader.Load<Texture2D>("res://addons/ReachForGodot/icons/prefab.png");
        } else if (target is GameObject) {
            icon.Texture = ResourceLoader.Load<Texture2D>("res://addons/ReachForGodot/icons/gear.png");
        } else if (target is SceneFolder) {
            icon.Texture = ResourceLoader.Load<Texture2D>("res://addons/ReachForGodot/icons/folder.png");
        } else {
            icon.Visible = false;
        }
    }

    [Signal] public delegate void PressedEventHandler();

    private Control HighlightNode => this;

    public void Setup(string? label, string? context, GodotObject target)
    {
        if (this.label != null) {
            this.label.Text = label;
        }
        if (this.context != null) {
            this.context.Text = context;
        }
        UpdateItemPreview(target);
    }

    public override void _Ready()
    {
        MouseEntered += OnItemMouseEntered;
        MouseExited += OnItemMouseExited;
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
        defaultStyleBox ??= HighlightNode.GetThemeStylebox("panel") ?? new StyleBoxFlat() { BgColor = Colors.Transparent };
        if (isPressed) {
            HighlightNode.AddThemeStyleboxOverride("panel", pressedStyleBox);
        } else if (isHovered) {
            HighlightNode.AddThemeStyleboxOverride("panel", hoverStyleBox);
        } else {
            HighlightNode.AddThemeStyleboxOverride("panel", defaultStyleBox);
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
                        EmitSignal(SignalName.Pressed);
                    }
                    isPressed = false;
                }
            }
        }
    }
}
