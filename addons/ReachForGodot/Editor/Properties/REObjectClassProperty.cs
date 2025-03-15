namespace ReaGE;

using System;
using System.Text.RegularExpressions;
using Godot;

[Tool, GlobalClass]
public partial class REObjectClassProperty : HBoxContainer
{
    private REObject? OwnerObject => inspector?.GetEditedObject() as REObject;
    private SupportedGame Game => (
        Target != null && Target.Game != SupportedGame.Unknown ? Target.Game
        : OwnerObject != null && OwnerObject.Game != SupportedGame.Unknown ? OwnerObject.Game
        : SupportedGame.Unknown);

    public REObject? Target { get; set; }

    private EditorProperty? inspector;
    private REField? field;
    private int arrayIndex = -1;

    private Control? classPicker;
    private string[] classnames = Array.Empty<string>();

    public override void _EnterTree()
    {
        SetupUI();
        // CallDeferred(MethodName.Setup);
    }

    private void Setup()
    {
        inspector = this.FindNodeInParents<EditorProperty>();
        field = inspector == null ? null : REObjectInspectorPlugin.FindFieldForInspector(inspector);
        SetupUI();
    }

    private void SetupUI()
    {
        var res = Target;
        if (res == null) {
            return;
        }

        string? baseclass = field?.RszField.original_type;
        if (string.IsNullOrEmpty(baseclass)) {
            if (res is UserdataResource) {
                baseclass = "via.UserData";
            }
            // if current class is component, allow other components?
        }

        if (!res.IsValid) {
            var gamePicker = new OptionButton() { Text = "Game", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var lineEdit = new LineEdit() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            classPicker = lineEdit;

            foreach (var game in ReachForGodot.GameList) {
                gamePicker.AddItem(game.ToString());
            }
            gamePicker.Selected = Array.IndexOf(ReachForGodot.GameList, res.Game);
            gamePicker.ItemSelected += OnChangeGame;
            AddChild(gamePicker);
            lineEdit.TextChanged += UpdateTargetClassname;

            AddChild(classPicker);
        } else {
            var opt = new OptionButton() { Text = "Classname", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            SetupClassSelection(opt, Target?.Classname ?? baseclass ?? string.Empty);
            if (opt.ItemCount > 1) {
                classPicker = opt;
                opt.ItemSelected += OnSelectedClassname;
                AddChild(classPicker);
            } else {
                var edit = new LineEdit() { Text = "Classname: " + classnames.First(), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                edit.Editable = false;
                AddChild(edit);
            }
        }
    }

    private void RefreshUI()
    {
        var updatePathBtn = GetNode<Button>("%UpdatePathBtn");
        // var targetAsset = (GetEditedObject() as IRszContainerNode)?.Asset ?? (GetEditedObject() as REResource)?.Asset;
        // var expectedImportPath = targetAsset?.GetImportFilepath(ReachForGodot.GetAssetConfig(Game))?.GetBaseName();
        // updatePathBtn.Visible = !string.IsNullOrEmpty(currentResourcePath) && !currentResourcePath.Equals(expectedImportPath, StringComparison.OrdinalIgnoreCase);
    }

    public void SetParentProperty(EditorProperty inspector, REField field, int arrayIndex = -1)
    {
        this.inspector = inspector;
        this.field = field;
        this.arrayIndex = arrayIndex;
        if (Target != null && Target.Game == SupportedGame.Unknown) {
            Target.Game = (inspector.GetEditedObject() as REObject)?.Game ?? SupportedGame.Unknown;
        }

        var baseclass = field.RszField.original_type;
        var options = classPicker as OptionButton;
        if (options == null) {
            options = new OptionButton() { Text = "Classname", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            options.ItemSelected += OnSelectedClassname;
        }
        if (string.IsNullOrEmpty(baseclass)) {
            baseclass = Target?.Classname;
        }

        if (string.IsNullOrEmpty(baseclass)) {
            GD.PrintErr("Could not determine REObject base class");
            return;
        }

        SetupClassSelection(options, baseclass);
        if (classPicker != null && classPicker != options) {
            classPicker.GetParent().EmplaceChild(classPicker, options);
            classPicker.QueueFree();
        }
        classPicker = options;
    }

    private void SetupClassSelection(OptionButton button, string baseclass)
    {
        button.Clear();
        classnames = Target?.AllowedSubclasses.Length > 0 ? Target.AllowedSubclasses : new[] { baseclass };
        var current = Target?.Classname;
        foreach (var cls in classnames) {
            button.AddItem(cls);
            if (cls == current) {
                button.Selected = button.ItemCount - 1;
            }
        }
        if (Target != null && Game != SupportedGame.Unknown && !classnames.Contains(Target.Classname)) {
            GD.Print($"Re-assigning REObject from invalid class {Target.Classname} to {classnames.First()}");
            UpdateTargetClassname(classnames.First());
        }
    }

    private void UpdateTargetClassname(string classname)
    {
        if (Target != null && Target.Classname == classname) {
            return;
        }
        var game = Game;
        if (TypeCache.GetRszClass(game, classname) == null) return;

        Type? specificType = null;
        if (Target != null && (specificType == null || specificType == Target.GetType())) {

            Target.ChangeClassname(classname);
            return;
        }

        if (game == SupportedGame.Unknown) {
            return;
        }

        if (inspector == null || field == null || inspector.GetEditedObject() is not REObject owner) {
            GD.PrintErr("Can't reassign object type " + classname + " as it requires a new instance and we don't have a known parent container object");
            return;
        }

        if (arrayIndex == -1) {
            owner.SetField(field, new REObject(game, classname));
        } else {
            owner.GetField(field).As<Godot.Collections.Array>()[arrayIndex] = new REObject(game, classname);
        }
    }

    private void OnChangeGame(long index)
    {
        if (Target == null) {
            return;
        }
        var newgame = ReachForGodot.GameList[index];
        if (Target.Game != newgame) {
            Target.Game = newgame;
            if (!string.IsNullOrEmpty(Target.Classname)) {
                Target.ResetProperties();
            }
        }
    }


    private void OnSelectedClassname(long index)
    {
        if (Target == null) {
            return;
        }
        var cls = classnames[index];
        if (Target.Classname != cls) {
            GD.Print("Changing classname: " + cls);
            UpdateTargetClassname(cls);
        }
    }

    // private void ValueChanged()
    // {
    //     var text = GetAsset()?.AssetFilename;
    //     var input = container.GetNode<LineEdit>("%Input");
    //     if (input.Text != text) {
    //         input.Text = text;
    //     }
    //     RefreshUI();
    // }
}