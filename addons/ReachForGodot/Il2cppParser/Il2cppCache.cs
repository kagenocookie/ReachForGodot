namespace ReaGE;

using System;
using System.Text.Json;
using Godot;
using REFDumpFormatter;

public class Il2cppCacheData
{
    public Dictionary<string, EnumCacheEntry> Enums { get; set; } = new();
    public Dictionary<string, List<string>> Subclasses { get; set; } = new();
}

public class EnumCacheEntry
{
    public string BackingType { get; set; } = string.Empty;
    public IEnumerable<EnumCacheItem> Items { get; set; } = Array.Empty<EnumCacheItem>();
}

public record EnumCacheItem(string name, JsonElement value);

public class Il2cppCache
{
    public readonly Dictionary<string, EnumDescriptor> enums = new();
    public Dictionary<string, List<string>> subclasses = new();

    private static Dictionary<string, Func<EnumDescriptor>> descriptorFactory = new();

    private static readonly HashSet<string> ignoredClassnames = new() {
        "System.Object", "System.ValueType", "System.MulticastDelegate", "System.Delegate", "System.Array"
    };

    private static Type? GetEnumBackingType(ObjectDef item)
    {
        if (item.fields == null) {
            return null;
        }

        foreach (var (fieldName, field) in item.fields) {
            if (fieldName == "value__") { // !IsStatic instead?
                return Type.GetType(field.Type);
            }
        }

        return typeof(int);
    }

    public void ApplyIl2cppData(SourceDumpRoot data)
    {
        enums.Clear();
        foreach (var (name, enumData) in data) {
            if (enumData.parent == null) continue;

            if (enumData.parent == "System.Enum") {
                var backing = GetEnumBackingType(enumData);
                if (backing == null) {
                    GD.PrintErr("Couldn't determine enum backing type: " + name);
                    enums[name] = EnumDescriptor<int>.Default;
                    continue;
                }

                var descriptor = CreateDescriptor(backing.FullName!);
                descriptor?.ParseIl2cppData(enumData);
                enums[name] = descriptor ?? EnumDescriptor<ulong>.Default;
            } else if (!name.Contains('!') && !ignoredClassnames.Contains(name) && !ignoredClassnames.Contains(enumData.parent) && !name.StartsWith("System.")) {
                var item = enumData;
                var parentName = name;

                // save only instantiable subclasses, ignore abstract / interfaces
                if (item.IsAbstract) continue;

                do {
                    if (item.parent == null) break;
                    if (!subclasses.TryGetValue(item.parent, out var list)) {
                        subclasses[item.parent] = list = new();
                        if (!item.IsAbstract) {
                            // non-abstract base types should also be saved as instantiable
                            list.Add(item.parent);
                        }
                    }

                    list.Add(name);
                }
                while (data.TryGetValue(item.parent, out item) && item.parent != null && !ignoredClassnames.Contains(item.parent));
            }
        }
    }

    private static EnumDescriptor? CreateDescriptor(string backing)
    {
        if (!descriptorFactory.TryGetValue(backing, out var fac)) {
            var t = Type.GetType(backing);
            if (t == null) {
                GD.PrintErr("Invalid cached enum backing type: " + backing);
                return null;
            }

            var enumType = typeof(EnumDescriptor<>).MakeGenericType(t!);
            fac = () => (EnumDescriptor)Activator.CreateInstance(enumType)!;
        }

        return fac();
    }

    public void ApplyCacheData(Il2cppCacheData data)
    {
        foreach (var (enumName, enumItem) in data.Enums) {
            if (!enums.TryGetValue(enumName, out var enumInstance)) {
                var descriptor = CreateDescriptor(enumItem.BackingType);
                descriptor?.ParseCacheData(enumItem.Items);
                enums[enumName] = descriptor ?? EnumDescriptor<ulong>.Default;
            }
        }
        subclasses = data.Subclasses;
    }

    public Il2cppCacheData ToCacheData()
    {
        var data = new Il2cppCacheData();
        foreach (var (name, entry) in enums) {
            var cacheEntry = new EnumCacheEntry();
            cacheEntry.BackingType = entry.BackingType.FullName!;
            cacheEntry.Items = entry.CacheItems;
            data.Enums.Add(name, cacheEntry);
        }
        data.Subclasses = subclasses;
        return data;
    }
}

