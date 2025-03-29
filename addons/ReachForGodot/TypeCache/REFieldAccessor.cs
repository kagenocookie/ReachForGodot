namespace ReaGE;

using System;
using Godot;
using RszTool;

public class REFieldAccessor
{
    public readonly string preferredName;
    private REFieldCondition[] conditions = Array.Empty<REFieldCondition>();
    private Dictionary<SupportedGame, REField?> _cache = new(1);
    public event Action<REField>? Override;
    public bool HasOverrides => Override != null;

    public REFieldAccessor(string name)
    {
        preferredName = name;
        conditions = [name];
    }

    public REFieldAccessor Conditions(params REFieldConditionFunc[] conditions)
    {
        this.conditions = conditions.Select(a => new REFieldCondition(a)).ToArray();
        return this;
    }

    public REFieldAccessor Type(RszFieldType type)
    {
        this.Override += (f) => f.RszField.type = type;
        return this;
    }

    public REFieldAccessor OriginalType(string classname, RszFieldType type = RszFieldType.Object)
    {
        this.Override += (f) => {
            f.RszField.original_type = classname;
            f.RszField.type = type;
        };
        return this;
    }

    public REFieldAccessor Resource<TRes>() where TRes : REResource
    {
        this.Override += (f) => f.MarkAsResource(typeof(TRes).Name);
        return this;
    }

    public REFieldAccessor CustomOverride(Action<REField> overrideFunc)
    {
        this.Override += overrideFunc;
        return this;
    }

    public REFieldAccessor Conditions(params REFieldCondition[] conditions)
    {
        this.conditions = conditions;
        return this;
    }

    public void Invoke(REField field)
    {
        Override?.Invoke(field);
    }

    public REField Get(REObject target) => Get(target.Game, target.TypeInfo);

    public bool IsMatch(REObject target, StringName name) => Get(target).SerializedName == name;

    public REField Get(SupportedGame game, ClassInfo typecache)
    {
        if (_cache.TryGetValue(game, out var cachedField)) {
            Debug.Assert(cachedField != null);
            return cachedField;
        }

        foreach (var getter in conditions) {
            cachedField = getter.func.Invoke(typecache.Fields)!;
            if (cachedField != null) {
                return _cache[game] = cachedField;
            }
        }

        throw new Exception("Failed to resolve " + typecache.RszClass.name + " field " + preferredName);
    }
}

public class REFieldCondition
{
    public REFieldConditionFunc func;

    public REFieldCondition(REFieldConditionFunc func)
    {
        this.func = func;
    }

    public static implicit operator REFieldCondition(string name)
        => new REFieldCondition((fs) => fs.FirstOrDefault(f => f.SerializedName == name));
    public static implicit operator REFieldCondition(REFieldConditionFunc condition)
        => new REFieldCondition(condition);
}

public delegate REField? REFieldConditionFunc(REField[] fields);
