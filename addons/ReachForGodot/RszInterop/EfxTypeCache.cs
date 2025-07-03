namespace ReaGE;

using System;
using System.Reflection;
using System.Text.Json;
using Godot;
using ReeLib;
using ReeLib.Data;
using ReeLib.Efx;
using ReeLib.Il2cpp;

public class ExtendedEfxClassInfo
{
    public Godot.Collections.Array<Godot.Collections.Dictionary> PropertyList { get; } = new();
    public EfxClassInfo BaseInfo { get; set; } = null!;

    public EfxStructInfo Info => BaseInfo.Info;
    public Dictionary<string, EfxFieldInfo> Fields => BaseInfo.Fields;
    public bool HasVersionedConstructor => BaseInfo.HasVersionedConstructor;
    public FieldInfo[] FieldInfos => BaseInfo.FieldInfos;
}

public static partial class TypeCache
{
    private static readonly Dictionary<EfxVersion, ExtendedEfxCache> efxCache = new();

    private sealed class ExtendedEfxCache
    {
        public EfxCacheData sourceCache = null!;
        public Dictionary<string, ExtendedEfxClassInfo> ExtendedCache = new();
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

    private static ExtendedEfxCache GetEfxCacheRoot(EfxVersion version)
    {
        if (!efxCache.TryGetValue(version, out var data)) {
            var game = GetGameForEfxVersion(version);
            var config = ReachForGodot.GetAssetConfig(game);
            var baseData = config.Workspace.EfxCacheData;
            efxCache[version] = data = ReadEfxCache(config.Workspace, baseData);
        }
        return data;
    }

    private static ExtendedEfxCache ReadEfxCache(Workspace env, EfxCacheData baseData)
    {
        ExtendedEfxCache data = new();

        foreach (var (name, attr) in baseData.AttributeTypes) {
            var info = GenerateEfxClassInfo(env, attr, data);
            data.ExtendedCache[info.BaseInfo.Info.Classname] = info;
            // data.cache.Structs.Add(attr.Classname, info);
        }
        foreach (var (name, obj) in baseData.Structs) {
            var info = GenerateEfxClassInfo(env, obj, data);
            data.ExtendedCache[info.BaseInfo.Info.Classname] = info;
            // data.cache.Structs.Add(obj.Classname, info);
        }
        return data;
    }

    private static ExtendedEfxClassInfo GenerateEfxClassInfo(Workspace env, EfxClassInfo classInfo, ExtendedEfxCache data)
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
        var info = new ExtendedEfxClassInfo() {
            BaseInfo = classInfo,
        };

        var rf = new REField() { RszField = null!, FieldIndex = 0 };
        foreach (var field in classInfo.Info.Fields) {
            if (field.Flag == EfxFieldFlags.BitSet) {
                var props = new Godot.Collections.Dictionary();
                rf.VariantType = Variant.Type.PackedInt32Array;
                rf.Hint = default;
                rf.HintString = default;
                rf.ElementType = default;
                UpdateEfxFieldProperty(rf, field.Name, props);
                info.PropertyList.Add(props);
                fieldDict.Add(field.Name, field);
                continue;
            }
            if (field.Flag != EfxFieldFlags.None) continue;

            // RszFieldType.ukn_type is UndeterminedFieldType fields
            // they're all 0 in known available files, meaning they're likely meaningless for editing purposes, hide from UI
            if (field.FieldType == RszFieldType.ukn_type) continue;

            rf.HintString = default;
            rf.ElementType = default;
            rf.Hint = default;
            rf.VariantType = default;

            fieldDict.Add(field.Name, field);
            var genFieldType = field.FieldType == RszFieldType.Struct ? RszFieldType.Object : field.FieldType;
            RszFieldToGodotProperty(rf, env, genFieldType, field.IsArray, field.Classname ?? string.Empty);
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

    public static ExtendedEfxClassInfo GetEfxStructInfo(EfxVersion version, string classname)
    {
        var cache = GetEfxCacheRoot(version);

        if (cache.ExtendedCache.TryGetValue(classname, out var info)) {
            return info;
        }

        GD.PrintErr($"Unknown efx struct classname {classname}. A file may be out of date.");
        return new ExtendedEfxClassInfo();
    }

}