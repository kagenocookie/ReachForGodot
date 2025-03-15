#if TOOLS
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Godot;

namespace ReaGE;

public partial class REObjectInspectorPlugin : EditorInspectorPlugin, ISerializationListener
{
    private static PluginSerializationFixer serializationFixer = new();

    public void OnAfterDeserialize() { }
    public void OnBeforeSerialize() => serializationFixer.OnBeforeSerialize();

    private static readonly StringName EmptyString = string.Empty;
    private readonly Dictionary<GodotObject, bool> recursionDict = new();

    private static Dictionary<Node, REField> editorFieldstorage = new();

    public override bool _CanHandle(GodotObject @object)
    {
        return @object is REObject;
    }

    public override void _ParseBegin(GodotObject @object)
    {
        if (@object is REObject res) {
            var inspector = new REObjectClassProperty();
            inspector.Target = res;
            AddCustomControl(inspector);
            serializationFixer.Register(@object, inspector);
        }
        base._ParseBegin(@object);
    }

    public override bool _ParseProperty(GodotObject @object, Variant.Type type, string name, PropertyHint hintType, string hintString, PropertyUsageFlags usageFlags, bool wide)
    {
        if (name == nameof(REObject.Game)) {
            return true;
        }
        if (name == nameof(REObject.Classname)) {
            return true;
        }

        // if (hintType == PropertyHint.ResourceType && hintString == nameof(REObject) && @object is REObject owner) {
        //     var antiStackOverflowKey = owner;
        //     if (recursionDict.ContainsKey(antiStackOverflowKey)) {
        //         return true;
        //     }

        //     var field = owner.TypeInfo.FieldsByName[name];
        //     recursionDict[antiStackOverflowKey] = true;
        //     var inspector = EditorInspector.InstantiatePropertyEditor(
        //         @object,
        //         type,
        //         name,
        //         hintType,
        //         hintString,
        //         (uint)usageFlags,
        //         wide);
        //     editorFieldstorage[inspector] = field;
        //     recursionDict.Remove(antiStackOverflowKey);
        //     inspector.SetObjectAndProperty(@object, name);
        //     AddPropertyEditor(name, inspector);
        //     ModifyObjectInspector(inspector, field);
        //     return true;
        // }
        // if (type == Variant.Type.Array && hintType == PropertyHint.TypeString && @object is REObject owner2) {
        //     var typeSpan = hintString.AsSpan().Slice(hintString.LastIndexOf(':') + 1);
        //     if (typeSpan.SequenceEqual(nameof(REObject)) || typeSpan.SequenceEqual(nameof(UserdataResource))) {

        //         var antiStackOverflowKey = owner2;
        //         if (recursionDict.ContainsKey(antiStackOverflowKey)) {
        //             return true;
        //         }

        //         var field = owner2.TypeInfo.FieldsByName[name];
        //         recursionDict[antiStackOverflowKey] = true;
        //         var inspector = EditorInspector.InstantiatePropertyEditor(
        //             @object,
        //             type,
        //             name,
        //             hintType,
        //             hintString,
        //             (uint)usageFlags,
        //             wide);
        //         recursionDict.Remove(antiStackOverflowKey);
        //         inspector.SetObjectAndProperty(@object, name);
        //         AddPropertyEditor(name, inspector);
        //         ModifyArrayInspector(inspector, field);
        //         return true;
        //     }
        // }
        return base._ParseProperty(@object, type, name, hintType, hintString, usageFlags, wide);
    }

    private void ModifyArrayInspector(EditorProperty inspector, REField field)
    {
        // TODO this one's very complicated with the non-array approach, find a better solution
    }

    private void ModifyObjectInspector(EditorProperty inspector, REField field)
    {
        // ugly hack buuuut it mostly works
        // the point is, we need the parent object to tell the child object inspector which classes it should allow
        // another workaround would be to store a base class field on every REObject which... is not ideal either
        // since this is only needed for editor purposes, I think it's better this way
        // also storing context data in a static dict because non-static captures break assembly unload

        var callback = Callable.From(static (Node child) => {
            var inspector = (EditorProperty)child.GetParent();
            // GD.Print($"callback 1 w fields: {fieldContainer != null}... " + inspector.GetTreeStringPretty());
            var fieldContainer = (child as EditorInspector)?.GetChild(0).GetChild(0);
            if (fieldContainer != null && editorFieldstorage.ContainsKey(inspector)) {
                fieldContainer.Connect(Node.SignalName.ChildEnteredTree, Callable.From(static (Node child) => {
                    if (child is REObjectClassProperty classInspector) {
                        var inspector = child.RequireNodeInParents<EditorProperty>();
                        var field = editorFieldstorage.TryGetValue(inspector, out var data) ? data : default;
                        if (field != null) {
                            classInspector.SetParentProperty(inspector, field);
                        }
                    }
                }));
            }
        });

        inspector.Connect(Node.SignalName.ChildEnteredTree, callback);
        editorFieldstorage[inspector] = field;
    }

    internal static REField? FindFieldForInspector(EditorProperty inspector) => editorFieldstorage.TryGetValue(inspector, out var data) ? data : default;
}
#endif