namespace RGE;

using System;
using System.Text.Json;
using Godot;
using REFDumpFormatter;

public class Il2cppCacheData
{
    public Dictionary<string, EnumCacheEntry> Enums { get; set; } = new();
}

public class EnumCacheEntry
{
    public string BackingType { get; set; } = string.Empty;
    public IEnumerable<EnumCacheItem> Items { get; set; } = Array.Empty<EnumCacheItem>();
}

public record EnumCacheItem(string name, JsonElement value);

public class Il2cppCache
{
    public Dictionary<string, EnumDescriptor> enums = new();

    private static Dictionary<string, Func<EnumDescriptor>> descriptorFactory = new();

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

        return data;
    }
}

