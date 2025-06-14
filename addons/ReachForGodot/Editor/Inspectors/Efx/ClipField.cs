namespace ReaGE;

using Godot;
using RszTool.Efx.Structs.Common;

[GlobalClass, Tool]
public partial class ClipField : Control
{
    [Signal] public delegate void ClipTypeChangedEventHandler(ClipValueType type);
    [Signal] public delegate void ValuesChangedEventHandler();
    [Signal] public delegate void StructureChangedEventHandler();
    [Signal] public delegate void ToggledEventHandler(bool toggled);

    private Container expandedContainer = null!;
    private Container frameContainer = null!;
    private CheckBox toggleBox = null!;
    private ClipValueType valueType;
    private bool toggled;
    private EfxParsedClip? data;

    private Control? frameItem;

    public void Setup(bool enabled, string name, ClipValueType valueType, EfxParsedClip? data)
    {
        toggled = enabled;
        var isNew = this.expandedContainer == null;
        this.data = data;
        this.RequireChildByTypeRecursive<Label>().Text = name;
        expandedContainer = GetNode<Container>("%Expanded");
        if (isNew) {
            expandedContainer.Visible = false;
        }
        frameContainer = GetNode<Container>("%Frames");

        var assign = this.RequireChildByTypeRecursive<OptionButton>();
        assign.Selected = valueType == ClipValueType.Int ? 1 : 0;

        toggleBox = this.RequireChildByTypeRecursive<CheckBox>();
        toggleBox.ButtonPressed = toggled;

        UpdateFrameUI();
    }

    private void OnToggle(bool toggled)
    {
        if (toggled) {
            expandedContainer.Visible = true;
        }
        EmitSignal(SignalName.Toggled, toggled);
    }

    private static readonly FrameInterpolationType[] interpolations = [
        FrameInterpolationType.Type1,
        FrameInterpolationType.Type2,
        FrameInterpolationType.Type3,
        FrameInterpolationType.Bezier,
        FrameInterpolationType.Type13,
    ];

    private void UpdateFrameUI()
    {
        if (frameItem == null) {
            frameItem = frameContainer.GetChild<Control>(0);
            frameContainer.RemoveChild(frameItem);
        }

        if (data == null) {
            frameContainer.QueueFreeChildren();
            return;
        }
        var children = frameContainer.GetChildCount();

        for (int i = 0; i < data.frames.Count; ++i) {
            Control item;
            var isNew = false;
            if (i < children) {
                item = frameContainer.GetChild<Control>(i);
            } else {
                item = (Control)frameItem.Duplicate();
                frameContainer.AddChild(item);
                isNew = true;
            }
            var mainContainer = item.GetChild(0);
            var tangentContainer = item.GetChild<Control>(1);

            mainContainer.RequireChildByType<Label>().Text = $"Frame {i + 1}";
            var timeInput = mainContainer.GetNode<SpinBox>("Frame");
            var valueInput = mainContainer.GetNode<SpinBox>("Value");
            var frame = data.frames[i];
            var handleOption = mainContainer.RequireChildByType<OptionButton>();

            timeInput.SetValueNoSignal(frame.data.frameTime);
            valueInput.SetValueNoSignal(frame.data.AsFloat(data.valueType));
            handleOption.Selected = System.Array.IndexOf(interpolations, frame.data.type);
            tangentContainer.Visible = frame.data.type == FrameInterpolationType.Bezier;
            var tans = tangentContainer.FindChildrenByType<SpinBox>().ToArray();
            tans[0].SetValueNoSignal(frame.tangents.in_x);
            tans[1].SetValueNoSignal(frame.tangents.in_y);
            tans[2].SetValueNoSignal(frame.tangents.out_x);
            tans[3].SetValueNoSignal(frame.tangents.out_y);
            if (isNew) {
                var removeBtn = mainContainer.RequireChildByType<Button>();
                removeBtn.Pressed += () => RemoveFrame(item.GetIndex());
                timeInput.ValueChanged += OnUpdatedFrameTime;
                valueInput.ValueChanged += OnUpdatedFrameValue;
                handleOption.ItemSelected += (idx) => {
                    var type = interpolations[idx];
                    data.frames[item.GetIndex()].data.type = type;
                    tangentContainer.Visible = type == FrameInterpolationType.Bezier;
                    EmitSignal(SignalName.ValuesChanged);
                };
                tans[0].ValueChanged += (value) => OnUpdatedTangent(item.GetIndex(), value, 0);
                tans[1].ValueChanged += (value) => OnUpdatedTangent(item.GetIndex(), value, 1);
                tans[2].ValueChanged += (value) => OnUpdatedTangent(item.GetIndex(), value, 2);
                tans[3].ValueChanged += (value) => OnUpdatedTangent(item.GetIndex(), value, 3);
            }
        }
        frameContainer.FreeAllChildrenAfterIndex(data.frames.Count);
    }

    private void OnUpdatedFrameTime(double value)
    {
        if (data == null) return;
        for (int i = 0; i < data.frames.Count; ++i) {
            var item = frameContainer.GetChild<Control>(i);
            var mainContainer = item.GetChild(0);
            var timeInput = mainContainer.GetNode<SpinBox>("Frame");
            data.frames[i].data.frameTime = (float)timeInput.Value;
        }
        EmitSignal(SignalName.ValuesChanged);
    }

    private void OnUpdatedFrameValue(double value)
    {
        if (data == null) return;
        for (int i = 0; i < data.frames.Count; ++i) {
            var item = frameContainer.GetChild<Control>(i);
            var mainContainer = item.GetChild(0);
            var valueInput = mainContainer.GetNode<SpinBox>("Value");
            data.frames[i].data.FromFloat(data.valueType, (float)valueInput.Value);
        }
        EmitSignal(SignalName.ValuesChanged);
    }

    private void OnUpdatedTangent(int frame, double value, int tangentFieldIndex)
    {
        if (data == null) return;
        var frameData = data.frames[frame];
        if (tangentFieldIndex == 0) frameData.tangents.in_x = (float)value;
        if (tangentFieldIndex == 1) frameData.tangents.in_y = (float)value;
        if (tangentFieldIndex == 2) frameData.tangents.out_x = (float)value;
        if (tangentFieldIndex == 3) frameData.tangents.out_y = (float)value;
        // for (int i = 0; i < data.frames.Count; ++i) {
        //     var item = frameContainer.GetChild<Control>(i);
        //     var valueInput = item.GetNode<SpinBox>("Value");
        //     var box = item.GetChild(1).GetChild(tangentFieldIndex + 1);
        //     // var
        //     // data.frames[i].data.FromFloat(data.valueType, (float)valueInput.Value);
        // }
        EmitSignal(SignalName.ValuesChanged);
    }

    private void OnClipTypeChanged(int selectedIndex)
    {
        ClipValueType type = selectedIndex == 1 ? ClipValueType.Int : ClipValueType.Float;
        EmitSignal(SignalName.ClipTypeChanged, (int)type);
    }

    private void RemoveFrame(int frame)
    {
        if (data != null) {
            data.frames.RemoveAt(frame);
        }
        EmitSignal(SignalName.StructureChanged);
    }

    private void AddFrame()
    {
        if (!toggled || data == null) {
            toggleBox.ButtonPressed = true;
            return;
        }

        var frame = new EfxClipFrame() {
            type = FrameInterpolationType.Type2,
        };
        var prevFrame = data.frames.Last();
        if (data.frames.Count == 0) {
            frame.frameTime = 50;
            if (data.valueType == ClipValueType.Float) {
                frame.FloatValue = prevFrame.data.FloatValue;
            } else {
                frame.IntValue = prevFrame.data.IntValue;
            }
        } else {
            frame.frameTime = prevFrame.data.frameTime + 1;
        }
        data.frames.Add(new EfxParsedClipFrame() {
            data = frame
        });
        EmitSignal(SignalName.StructureChanged);
    }

    private void ToggleExpansion()
    {
        if (expandedContainer == null) return;
        expandedContainer.Visible = !expandedContainer.Visible;
        if (expandedContainer.Visible) {
            UpdateFrameUI();
        }
    }

    protected override void Dispose(bool disposing)
    {
        frameItem?.QueueFree();
        base.Dispose(disposing);
    }
}