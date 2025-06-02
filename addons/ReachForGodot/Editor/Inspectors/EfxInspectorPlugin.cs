#if TOOLS
using System.Reflection;
using Godot;
using RszTool.Efx;
using RszTool.Efx.Structs.Common;

namespace ReaGE;

[CustomInspectorBaseTypes(typeof(EfxObject))]
public partial class EfxInspectorPlugin : CommonInspectorPluginBase
{
    private Dictionary<string, OverrideInfo> overridesBegin = new();
    private Dictionary<string, OverrideInfo> overridesEnd = new();
    private Dictionary<string, OverrideInfo> overridesProperty = new();

    private sealed record OverrideInfo(MethodInfo handler, bool preventOriginal);

    private static Lazy<PackedScene> ExpressionFieldScene = new Lazy<PackedScene>(() => ResourceLoader.Load<PackedScene>("res://addons/ReachForGodot/Editor/Inspectors/Efx/ExpressionField.tscn"));

    protected override string? GetIdentifier(GodotObject target)
    {
        if (target is not EfxObject efx) return null;
        var tags = efx.ClassTags;
        if (tags.Length != 0) return tags[0];

        var basename = efx.ClassBaseName;
        return basename;
    }

    [CustomInspectorTarget("UnitCulling")]
    protected void HandleUnitCulling_Begin(EfxObject target)
    {
        AddCustomControl(new Button() { Text = "Unit Culling TEST" });
    }

    [CustomInspectorTarget(EfxObject.ClassTagExpressionContainer)]
    protected bool ExpressionContainer_Begin(EfxObject target)
    {
        if (target.RuntimeObject is IExpressionAttribute attr) {
            var owner = EditorInterface.Singleton.GetEditedSceneRoot();
            if (owner is not IExpressionParameterSource efxroot) {
                AddCustomControl(new Label() { Text = "Active scene root is not a valid EFX parameter source" });
                return false;
            }

            // TODO extract expression edit handling to proper wrapper class
            // - figure out expression bit versioning
            // - add a convenience "Add Expression Parameter to EFX root" button

            AssetConverter.Instance.Game = target.Version.GetGameForEfxVersion();
            if (attr.Expression == null) {
                attr.Expression = new RszTool.Efx.Structs.Common.EFXExpressionList(target.Version);
                AssetConverter.Instance.Efx.ExportObject(attr, target);
            }

            if (attr.Expression.ParsedExpressions == null) {
                attr.Expression.ParsedExpressions = EfxExpressionTreeUtils.ReconstructExpressionTreeList(attr.Expression.Expressions, efxroot);
            }

            var set = 0;
            for (int i = 0; i < attr.ExpressionBits.BitCount; ++i) {
                int myBitIndex = i;
                var name = attr.ExpressionBits.GetBitName(myBitIndex) ?? ("Field " + (i + 1));
                var ctrl = ExpressionFieldScene.Value.Instantiate<ExpressionField>();
                var enabled = attr.ExpressionBits.HasBit(myBitIndex);
                var fieldInfoStartIndex = target.ClassBaseName == "Velocity3DExpression" && target.Version != EfxVersion.RE8 ? 5 : 1;
                ctrl.Setup(
                    enabled,
                    name,
                    target.TypeInfo.FieldInfos[fieldInfoStartIndex + i].GetValue(attr) is ExpressionAssignType type ? type : 0,
                    enabled ? attr.Expression.ParsedExpressions[set++] : null);
                ctrl.AssignTypeChanged += (type) => {
                    target.TypeInfo.FieldInfos[fieldInfoStartIndex + myBitIndex].SetValue(attr, type);
                    target.Set(target.TypeInfo.FieldInfos[fieldInfoStartIndex + myBitIndex].Name, (int)type);
                    target.NotifyPropertyListChanged();
                };
                ctrl.ExpressionConfirmed += (text) => {
                    var index = attr.ExpressionBits.GetBitInsertIndex(myBitIndex);
                    var parsed = EfxExpressionStringParser.Parse(text, attr.Expression.Expressions[index].parameters ?? new(0));
                    attr.Expression.Expressions[index].parameters = parsed.parameters.ToList();
                    EfxExpressionTreeUtils.FlattenExpressions(attr.Expression.Expressions[index].components, parsed, efxroot);
                    attr.Expression.ParsedExpressions[index] = parsed;
                    AssetConverter.Instance.Efx.AssignObject(target.Get("expressions").As<EfxObject>(), attr.Expression);
                };
                ctrl.ExpressionToggled += (toggle) => {
                    var isSet = attr.ExpressionBits.HasBit(myBitIndex);
                    if (isSet == toggle) return;

                    var index = attr.ExpressionBits.GetBitInsertIndex(myBitIndex);
                    attr.ExpressionBits.SetBit(myBitIndex, toggle);
                    if (toggle) {
                        // add new empty expression
                        attr.Expression.Expressions.Insert(index, new EFXExpressionObject(target.Version));
                        attr.Expression.ParsedExpressions.Insert(index, new EFXExpressionTree());
                    } else {
                        // remove expression
                        attr.Expression.Expressions.RemoveAt(index);
                        attr.Expression.ParsedExpressions.RemoveAt(index);
                    }
                    AssetConverter.Instance.Efx.AssignObject(target.Get("expressions").As<EfxObject>(), attr.Expression);
                    target.Set("expressionBits", attr.ExpressionBits.Bits);
                    target.NotifyPropertyListChanged();
                };
                AddCustomControl(ctrl);
            }

            // return true; // leaving it false for now so we can see the actual data behind our UI
        } else {
            AddCustomControl(new Label() { Text = "Unsupported expression container" });
        }
        return false;
    }

    // [CustomInspectorPropertyTarget(null, EfxObject.ClassTagExpressionList, false)]
    // protected void HandleExpressionListProperty(EfxObject target, string property)
    // {
    //     var attr = target;
    //     if (target.Get(property).TryCast<EfxObject>(out var expressionList)) {
    //         AddCustomControl(new Button() { Text = "Expression list!!!" });
    //     } else {
    //         AddCustomControl(new Button() { Text = "invalid Expression list!!!" });
    //     }
    // }
}

#endif