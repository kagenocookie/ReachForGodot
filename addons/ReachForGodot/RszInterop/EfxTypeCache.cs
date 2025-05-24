namespace ReaGE;

using System;
using System.Numerics;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using RszTool;
using RszTool.Efx;
using RszTool.Tools;

public class EfxClassInfo
{
    public EfxStructInfo Info { get; set; } = null!;
    public Dictionary<string, EfxFieldInfo> Fields { get; set; } = new();
    public Godot.Collections.Array<Godot.Collections.Dictionary> PropertyList { get; } = new();
    public bool HasVersionedConstructor { get; set; }

    private FieldInfo[]? _fieldInfos;
    public FieldInfo[] FieldInfos => _fieldInfos ??= GetFieldInfos();

    private FieldInfo[] GetFieldInfos()
    {
        var arr = new FieldInfo[Fields.Count];

        var targetType = typeof(EFXAttribute).Assembly.GetType(Info.Classname)
            ?? throw new Exception("Invalid efx target type " + Info.Classname);

        int i = 0;
        foreach (var f in Fields) {
            arr[i++] = targetType.GetField(f.Key, BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
                ?? throw new Exception("Invalid efx field " + f.Value.Name);
        }

        return arr;
    }
}

public static partial class TypeCache
{
    private static readonly Dictionary<EfxVersion, EfxCacheData> efxCache = new();

    private sealed class EfxCacheData
    {
        public Dictionary<string, EfxClassInfo> AttributeTypes = new();
        public Dictionary<string, EfxClassInfo> Structs = new();
        public Dictionary<string, EnumDescriptor> Enums = new();
        public GameClassCache classCache = null!;
    }

    public static SupportedGame GetGameForEfxVersion(this EfxVersion version)
    {
        return version switch {
            EfxVersion.DD2 => SupportedGame.DragonsDogma2,
            EfxVersion.DMC5 => SupportedGame.DevilMayCry5,
            EfxVersion.MHRise => SupportedGame.MonsterHunterRise,
            EfxVersion.MHRiseSB => SupportedGame.MonsterHunterRise,
            EfxVersion.MHWilds => SupportedGame.MonsterHunterWilds,
            EfxVersion.RE2 => SupportedGame.ResidentEvil2,
            EfxVersion.RE3 => SupportedGame.ResidentEvil3,
            EfxVersion.RE4 => SupportedGame.ResidentEvil4,
            EfxVersion.RE7 => SupportedGame.ResidentEvil7,
            EfxVersion.RE8 => SupportedGame.ResidentEvil8,
            EfxVersion.SF6 => SupportedGame.StreetFighter6,
            EfxVersion.RERT => SupportedGame.ResidentEvil2RT,
            _ => SupportedGame.Unknown,
        };
    }

    public static EfxVersion GameToEfxVersion(this SupportedGame game)
    {
        return game switch {
            SupportedGame.DragonsDogma2 => EfxVersion.DD2,
            SupportedGame.DevilMayCry5 => EfxVersion.DMC5,
            SupportedGame.MonsterHunterRise => EfxVersion.MHRiseSB,
            SupportedGame.MonsterHunterWilds => EfxVersion.MHWilds,
            SupportedGame.ResidentEvil2 => EfxVersion.RE2,
            SupportedGame.ResidentEvil3 => EfxVersion.RE3,
            SupportedGame.ResidentEvil4 => EfxVersion.RE4,
            SupportedGame.ResidentEvil7 => EfxVersion.RE7,
            SupportedGame.ResidentEvil8 => EfxVersion.RE8,
            SupportedGame.StreetFighter6 => EfxVersion.SF6,
            SupportedGame.ResidentEvil2RT => EfxVersion.RERT,
            _ => EfxVersion.Unknown,
        };
    }

    private static EfxCacheData GetEfxCacheRoot(EfxVersion version)
    {
        if (!efxCache.TryGetValue(version, out var data)) {
            var game = GetGameForEfxVersion(version);
            var cacheFile = ReachForGodot.GetPaths(game)?.EfxStructsFilepath;
            if (cacheFile != null) {
                if (version == EfxVersion.MHRise) {
                    cacheFile = cacheFile.Replace(".json", "_base.json");
                }

                if (File.Exists(cacheFile)) {
                    data = ReadEfxCache(cacheFile);
                }
            }
            efxCache[version] = data ??= new();
        }
        return data;
    }

    private static EfxCacheData? ReadEfxCache(string cacheFile)
    {
        // TODO move RszTool.Tools into the base rsztool project, as we don't wanna ship that data in the .dll
        EfxCacheData? data;
        using var f = File.OpenRead(cacheFile);
        var cache = JsonSerializer.Deserialize<EfxStructCache>(f, jsonOptions);
        data = new EfxCacheData();
        if (cache == null) return null;
        data.classCache = new (SupportedGame.Unknown);
        data.classCache.SetupEfxData(cache);

        foreach (var (name, attr) in cache.AttributeTypes) {
            EfxClassInfo info = GenerateEfxClassInfo(attr, data);
            data.AttributeTypes[name] = info;
            data.Structs.Add(attr.Classname, info);
        }
        foreach (var (name, obj) in cache.Structs) {
            EfxClassInfo info = GenerateEfxClassInfo(obj, data);
            data.Structs.Add(obj.Classname, info);
        }
        return data;
    }

    private static EfxClassInfo GenerateEfxClassInfo(EfxStructInfo attr, EfxCacheData data)
    {
        static void UpdateEfxFieldProperty(REField field, string name, Godot.Collections.Dictionary dict)
        {
            dict["name"] = name;
            dict["type"] = (int)field.VariantType;
            dict["hint"] = (int)field.Hint;
            dict["usage"] = (int)(PropertyUsageFlags.Editor|PropertyUsageFlags.ScriptVariable);
            if (field.HintString != null) dict["hint_string"] = field.HintString;
        }

        var fieldDict = new Dictionary<string, EfxFieldInfo>();
        var info = new EfxClassInfo() {
            Fields = fieldDict,
            Info = attr,
            HasVersionedConstructor =
                !string.IsNullOrEmpty(attr.Classname) &&
                typeof(EFXAttribute).Assembly.GetType(attr.Classname)?.GetConstructor([typeof(EfxVersion)]) != null,
        };

        var rf = new REField() { RszField = null!, FieldIndex = 0 };
        foreach (var field in attr.Fields) {
            if (field.Flag != EfxFieldFlags.None) continue;

            // RszFieldType.ukn_type is UndeterminedFieldType fields
            // they're all 0 in available files, meaning they're likely meaningless for editing purposes, hide from UI
            if (field.FieldType == RszFieldType.ukn_type) continue;

            rf.HintString = default;
            rf.ElementType = default;
            rf.Hint = default;
            rf.VariantType = default;

            fieldDict.Add(field.Name, field);
            var genFieldType = field.FieldType == RszFieldType.Struct ? RszFieldType.Object : field.FieldType;
            RszFieldToGodotProperty(rf, data.classCache, genFieldType, field.IsArray, field.Classname ?? string.Empty);
            var propertyDict = new Godot.Collections.Dictionary();
            UpdateEfxFieldProperty(rf, field.Name, propertyDict);
            info.PropertyList.Add(propertyDict);
        }
        return info;
    }

    public static bool EfxStructExists(EfxVersion version, string classname)
    {
        return GetEfxStructInfo(version, classname) != null;
    }

    public static EfxClassInfo GetEfxStructInfo(EfxVersion version, string classname)
    {
        var cache = GetEfxCacheRoot(version);

        if (cache.Structs.TryGetValue(classname, out var info)) {
            return info;
        }

        throw new ArgumentException("Unknown efx struct classname " + classname);
    }

}