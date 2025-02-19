namespace RGE;

using System;
using System.Text.Json;
using Godot;

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

    public void ApplyIl2cppData(REFDumpFormatter.SourceDumpRoot data)
    {
        enums.Clear();
        foreach (var e in data) {
            if (e.Value.parent == "System.Enum") {
                var backing = REFDumpFormatter.EnumParser.GetEnumBackingType(e.Value);
                if (backing == null) {
                    GD.PrintErr("Couldn't determine enum backing type: " + e.Key);
                    enums[e.Key] = EnumDescriptor<int>.Default;
                    continue;
                }

                var descriptor = CreateDescriptor(backing);
                descriptor.ParseIl2cppData(e.Value);
                enums[e.Key] = descriptor;
            }
        }
    }

    private static EnumDescriptor CreateDescriptor(Type backing)
    {
        var enumType = typeof(EnumDescriptor<>).MakeGenericType(backing);
        var descriptor = (EnumDescriptor)Activator.CreateInstance(enumType)!;
        return descriptor;
    }

    public void ApplyCacheData(Il2cppCacheData data)
    {
        foreach (var (enumName, enumItem) in data.Enums) {
            if (!enums.TryGetValue(enumName, out var enumInstance)) {
                var backing = Type.GetType(enumItem.BackingType);
                if (backing == null) {
                    GD.PrintErr("Invalid cached enum backing type: " + enumItem.BackingType);
                    enums[enumName] = EnumDescriptor<int>.Default;
                    continue;
                }

                var descriptor = CreateDescriptor(backing);
                descriptor.ParseCacheData(enumItem.Items);
                enums[enumName] = descriptor;
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

