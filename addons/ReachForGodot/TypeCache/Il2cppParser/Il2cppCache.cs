namespace ReaGE;

using System;
using System.Text.Json;
using Godot;
using REFDumpFormatter;

public class Il2cppCacheData
{
    public static readonly int CurrentCacheVersion = 2;

    public int CacheVersion { get; set; }
    public Dictionary<string, EnumCacheEntry> Enums { get; set; } = new();
    public Dictionary<string, List<string>> Subclasses { get; set; } = new();
}

public class EnumCacheEntry
{
    public bool IsFlags { get; set; }
    public string BackingType { get; set; } = string.Empty;
    public IEnumerable<EnumCacheItem> Items { get; set; } = Array.Empty<EnumCacheItem>();
}

public class TypePatch
{
    public bool IsFlagEnum { get; set; }
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
                        if (!data[item.parent].IsAbstract) {
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

    public bool ApplyPatches(Dictionary<string, TypePatch> patches)
    {
        var changed = false;
        foreach (var (cls, patch) in patches) {
            if (patch.IsFlagEnum && enums.TryGetValue(cls, out var enumDesc) && !enumDesc.IsFlags) {
                enumDesc.IsFlags = true;
                changed = true;
            }
        }
        return changed;
    }

    private static EnumDescriptor? CreateDescriptor(string backing)
    {
        if (!descriptorFactory.TryGetValue(backing, out var factory)) {
            var t = Type.GetType(backing);
            if (t == null) {
                GD.PrintErr("Invalid cached enum backing type: " + backing);
                return null;
            }

            var enumType = typeof(EnumDescriptor<>).MakeGenericType(t!);
            descriptorFactory[backing] = factory = () => (EnumDescriptor)Activator.CreateInstance(enumType)!;
        }

        return factory();
    }

    public void ApplyCacheData(Il2cppCacheData data)
    {
        foreach (var (enumName, enumItem) in data.Enums) {
            if (!enums.TryGetValue(enumName, out var enumInstance)) {
                var descriptor = CreateDescriptor(enumItem.BackingType);
                if (descriptor != null) {
                    descriptor.ParseCacheData(enumItem.Items);
                    descriptor.IsFlags = enumItem.IsFlags;
                }
                enums[enumName] = descriptor ?? EnumDescriptor<ulong>.Default;
            }
        }
        subclasses = data.Subclasses;
    }

    public Il2cppCacheData ToCacheData()
    {
        var data = new Il2cppCacheData() { CacheVersion = Il2cppCacheData.CurrentCacheVersion };
        foreach (var (name, entry) in enums) {
            var cacheEntry = new EnumCacheEntry();
            cacheEntry.BackingType = entry.BackingType.FullName!;
            cacheEntry.Items = entry.CacheItems;
            cacheEntry.IsFlags = entry.IsFlags;
            data.Enums.Add(name, cacheEntry);
        }
        data.Subclasses = subclasses;
        return data;
    }
}

