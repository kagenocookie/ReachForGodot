#if TOOLS
using System.Reflection;
using Godot;

namespace ReaGE;

public abstract partial class CommonInspectorPluginBase : EditorInspectorPlugin, ISerializationListener
{
    private static PluginSerializationFixer serializationFixer = new();

    public virtual void OnAfterDeserialize() { }
    public virtual void OnBeforeSerialize() => serializationFixer.OnBeforeSerialize();

    private Dictionary<string, OverrideInfo> overridesBegin = new();
    private Dictionary<string, OverrideInfo> overridesEnd = new();
    private Dictionary<string, OverrideInfo> overridesFull = new();
    // private Dictionary<string, OverrideInfo> overridesProperty = new();
    private Dictionary<string, List<PropertyOverrideInfo>> overridesProperty = new();
    private HashSet<Type>? propertyOwnerBaseTypes;

    private record OverrideInfo(MethodInfo handler, bool preventOriginal);
    private record PropertyOverrideInfo(MethodInfo handler, string? ownerIdentifier, bool preventOriginal) : OverrideInfo(handler, preventOriginal);

    public CommonInspectorPluginBase()
    {
        SetupOverrides();
    }

    public override bool _CanHandle(GodotObject @object)
    {
        if (propertyOwnerBaseTypes != null) {
            if (!propertyOwnerBaseTypes.Contains(@object.GetType())) {
                return false;
            }
            if (overridesProperty.Count != 0) return true;
        }

        var id = GetIdentifier(@object);
        if (id == null) return false;
        return overridesBegin.ContainsKey(id) || overridesEnd.ContainsKey(id) || overridesFull.ContainsKey(id) || overridesProperty.ContainsKey(id);
    }

    private void SetupOverrides()
    {
        var baseTypeAttr = GetType().GetCustomAttribute<CustomInspectorBaseTypesAttribute>();
        if (baseTypeAttr != null) {
            propertyOwnerBaseTypes = baseTypeAttr.types.ToHashSet();
        }
        var methods = GetType().GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        foreach (var m in methods) {
            foreach (var attr in m.GetCustomAttributes<CustomInspectorTargetAttribute>()) {
                if (m.Name.EndsWith("begin", StringComparison.OrdinalIgnoreCase)) {
                    overridesBegin.Add(attr.identifier, new OverrideInfo(m, attr.overrideOriginal));
                }
                if (m.Name.EndsWith("end", StringComparison.OrdinalIgnoreCase)) {
                    overridesEnd.Add(attr.identifier, new OverrideInfo(m, attr.overrideOriginal));
                }
                if (m.Name.EndsWith("full", StringComparison.OrdinalIgnoreCase)) {
                    overridesFull.Add(attr.identifier, new OverrideInfo(m, attr.overrideOriginal));
                }
                if (m.Name.EndsWith("property", StringComparison.OrdinalIgnoreCase)) {
                    if (!overridesProperty.TryGetValue(attr.identifier, out var info)) {
                        overridesProperty[attr.identifier] = info = new List<PropertyOverrideInfo>();
                    }
                    info.Add(new PropertyOverrideInfo(m, null, attr.overrideOriginal));
                }
            }

            foreach (var attr in m.GetCustomAttributes<CustomInspectorPropertyTargetAttribute>()) {
                if (!overridesProperty.TryGetValue(attr.targetTypeIdentifier, out var info)) {
                    overridesProperty[attr.targetTypeIdentifier] = info = new List<PropertyOverrideInfo>();
                }
                info.Add(new PropertyOverrideInfo(m, attr.sourceIdentifier, attr.overrideOriginal));
                // if (attr.ownerBaseType != null) {
                //     propertyOwnerBaseTypes.Add(attr.ownerBaseType);
                // }
            }
        }
    }

    protected abstract string? GetIdentifier(GodotObject target);

    public override void _ParseBegin(GodotObject @object)
    {
        var id = GetIdentifier(@object);
        if (id != null) {
            if (overridesBegin.TryGetValue(id, out var info)) {
                HandleOverride(@object, info);
            }
            if (overridesFull.TryGetValue(id, out info)) {
                HandleOverride(@object, info);
            }
        }
        base._ParseBegin(@object);
    }

    public override void _ParseEnd(GodotObject @object)
    {
        var id = GetIdentifier(@object);
        if (id != null && overridesEnd.TryGetValue(id, out var info)) {
            HandleOverride(@object, info);
        }
        base._ParseBegin(@object);
    }

    public override bool _ParseProperty(GodotObject @object, Variant.Type type, string name, PropertyHint hintType, string hintString, PropertyUsageFlags usageFlags, bool wide)
    {
        var id = GetIdentifier(@object);
        if (id != null) {
            if (overridesFull.ContainsKey(id)) return true;
        }

        var prop = @object.Get(name);
        if (prop.VariantType == Variant.Type.Object) {
            var propId = GetIdentifier(prop.AsGodotObject());
            if (propId != null && overridesProperty.TryGetValue(propId, out var propinfolist)) {
                foreach (var info in propinfolist) {
                    if (info.ownerIdentifier == null || info.ownerIdentifier == id) {
                        if (HandlePropertyOverride(@object, name, info)) {
                            return true;
                        }
                    }
                }
            }
        }
        return base._ParseProperty(@object, type, name, hintType, hintString, usageFlags, wide);
    }

    private bool HandleOverride(GodotObject target, OverrideInfo info)
    {
        var retval = info.handler.Invoke(this, [target]);
        return info.preventOriginal || retval is bool retbool && retbool;
    }

    private bool HandlePropertyOverride(GodotObject target, string property, OverrideInfo info)
    {
        var retval = info.handler.Invoke(this, [target, property]);
        return info.preventOriginal || retval is bool retbool && retbool;
    }
}

[System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
sealed class CustomInspectorBaseTypesAttribute : System.Attribute
{
    public readonly Type[] types;

    public CustomInspectorBaseTypesAttribute(params Type[] types)
    {
        this.types = types;
    }
}
[System.AttributeUsage(System.AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
sealed class CustomInspectorTargetAttribute : System.Attribute
{
    public readonly string identifier;
    public readonly bool overrideOriginal;

    public CustomInspectorTargetAttribute(string identifier, bool overrideOriginal = false)
    {
        this.identifier = identifier;
        this.overrideOriginal = overrideOriginal;
    }
}
[System.AttributeUsage(System.AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
sealed class CustomInspectorPropertyTargetAttribute : System.Attribute
{
    public readonly string? sourceIdentifier;
    public readonly string targetTypeIdentifier;
    public readonly bool overrideOriginal;

    public CustomInspectorPropertyTargetAttribute(string? sourceIdentifier, string fieldTypeIdentifier, bool overrideOriginal = false)
    {
        this.targetTypeIdentifier = fieldTypeIdentifier;
        this.overrideOriginal = overrideOriginal;
    }
}
#endif