namespace ReaGE;

using System;
using Godot;
using RszTool;

public class REFieldAccessor
{
    public readonly string preferredName;
    private REFieldCondition[] conditions = Array.Empty<REFieldCondition>();
    private Dictionary<SupportedGame, REField?> _cache = new(1);
    public Action<REField>? overrideFunc;

    public REFieldAccessor(string name, Action<REField>? overrideFunc = null)
    {
        preferredName = name;
        conditions = [name];
        this.overrideFunc = overrideFunc;
    }

    public REFieldAccessor(string name, RszFieldType rszType)
    {
        preferredName = name;
        this.overrideFunc = (f) => {
            f.RszField.type = rszType;
        };
    }

    public REFieldAccessor(string name, Type godotResourceType)
    {
        preferredName = name;
        Debug.Assert(godotResourceType.IsAssignableTo(typeof(REResource)));
        this.overrideFunc = (f) => f.MarkAsResource(godotResourceType.Name);
    }

    public REFieldAccessor(string name, string objectOriginalType)
    {
        preferredName = name;
        this.overrideFunc = (f) => {
            f.RszField.type = RszFieldType.Object;
            f.RszField.original_type = objectOriginalType;
        };
    }

    public REFieldAccessor WithConditions(params REFieldConditionFunc[] conditions)
    {
        this.conditions = conditions.Select(a => new REFieldCondition(a)).ToArray();
        return this;
    }

    public REFieldAccessor WithConditions(params REFieldCondition[] conditions)
    {
        this.conditions = conditions;
        return this;
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
