namespace ReaGE;

using System;
using Godot;
using RszTool.Efx.Structs.Common;

[GlobalClass, Tool]
public partial class ExpressionField : Control
{
    [Signal] public delegate void ExpressionConfirmedEventHandler(string text);
    [Signal] public delegate void ExpressionToggledEventHandler(bool toggle);
    [Signal] public delegate void AssignTypeChangedEventHandler(ExpressionAssignType type);

    private string? originalExpression;
    private TextEdit edit = null!;
    private EFXExpressionTree? expression;
    private bool toggled;

    public void Setup(bool enabled, string name, ExpressionAssignType assignType, EFXExpressionTree? expression)
    {
        toggled = enabled;
        this.expression = expression;
        this.RequireChildByTypeRecursive<Label>().Text = name;
        edit = this.RequireChildByTypeRecursive<TextEdit>();
        var assign = this.RequireChildByTypeRecursive<OptionButton>();
        assign.Selected = assignType == ExpressionAssignType.ForceWord ? 5 : (int)assignType;
        this.RequireChildByTypeRecursive<CheckBox>().ButtonPressed = toggled;
        if (expression != null) {
            var expr = expression;
            edit.Text = originalExpression = expression.ToString();
        } else {
            edit.Text = originalExpression = "";
        }
        GetNode<Control>("%EditControls").Visible = false;
        SetError(null);
    }

    private void OnAssignTypeChanged(int selectedIndex)
    {
        ExpressionAssignType type = selectedIndex == 5 ? ExpressionAssignType.ForceWord : (ExpressionAssignType)selectedIndex;
        EmitSignal(SignalName.AssignTypeChanged, (int)type);
    }

    private void OnExpressionTextChanged()
    {
        if (edit.Text == originalExpression) {
            GetNode<Control>("%EditControls").Visible = false;
        } else {
            GetNode<Control>("%EditControls").Visible = true;
            SetError(GetExpressionError());
        }
    }

    private void OnToggled(bool toggled)
    {
        this.toggled = toggled;
        EmitSignal(SignalName.ExpressionToggled, toggled);
    }

    private string? GetExpressionError()
    {
        try {
            expression ??= new();
            expression = EfxExpressionStringParser.Parse(edit.Text, expression.parameters);
            return null;
        } catch (Exception e) {
            return "Syntax error: " + e.Message;
        }
    }

    private void SetError(string? error)
    {
        if (error == null) {
            GetNode<Control>("%ErrorText").Visible = false;
        } else {
            GetNode<Control>("%ErrorText").Visible = true;
            GetNode<Label>("%ErrorText").Text = error;
        }
    }

    private void ConfirmExpression()
    {
        var err = GetExpressionError();
        SetError(err);
        if (err != null) return;

        if (!toggled) {
            SetError("Expression is not enabled!");
            return;
        }

        EmitSignal(SignalName.ExpressionConfirmed, originalExpression = edit.Text);
        RevertExpression();
    }

    private void RevertExpression()
    {
        edit.Text = originalExpression;
        OnExpressionTextChanged();
    }
}