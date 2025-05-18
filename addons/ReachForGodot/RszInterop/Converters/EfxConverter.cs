namespace ReaGE;

using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using ReaGE.EFX;
using RszTool;
using RszTool.Efx;
using RszTool.Efx.Structs.RE4;
using RszTool.Efx.Structs.RERT;
using RszTool.Tools;

public class EfxConverter : SceneRszAssetConverter<EfxResource, EfxFile, EfxRootNode>
{
    public override EfxResource CreateOrReplaceResourcePlaceholder(AssetReference reference)
        => SetupResource(new EfxResource(), reference);

    public override EfxFile CreateFile(FileHandler fileHandler) => new EfxFile(fileHandler);

    protected override void PreCreateScenePlaceholder(EfxRootNode node, EfxResource target)
    {
        node.Resource = target;
    }

    public override Task<bool> Import(EfxFile file, EfxRootNode target)
        => Task.FromResult(ImportSync(file, target));

    public bool ImportSync(EfxFile file, EfxRootNode target)
    {

        if (file.Header == null) return false;

        var resource = target.Asset != null ? target.Resource : null;
        if (resource == null) {
            resource = new EfxResource();
            if (WritesEnabled && target.Asset != null) resource.SaveOrReplaceResource(target.Asset.GetImportFilepath(Config)!);
            target.Resource = resource;
        }
        target.LockNode(true);
        target.Version = file.Header.Version;
        target.Flags = file.Header.uknFlag;
        target.UnknownNum = file.Header.ukn;
        foreach (var srcEntry in file.Entries) {
            var entry = target.Entries.FirstOrDefault(e => e.OriginalName == srcEntry.name);
            if (entry == null) {
                entry = new EfxNode() {
                    Name = srcEntry.name ?? ("Effect_" + srcEntry.index),
                };
                target.AddUniqueNamedChild(entry);
                entry.Owner = target.Owner ?? target;
            }
            entry.index = srcEntry.index;
            entry.OriginalName = srcEntry.name;
            entry.assignment = srcEntry.entryAssignment;
            entry.LockNode(true);

            foreach (var srcAttr in srcEntry.Attributes) {
                ImportAttribute(file, target, entry, srcAttr);
            }

            entry.RegenerateNodeName();
        }

        foreach (var srcAction in file.Actions) {
            var action = target.Actions.FirstOrDefault(e => e.OriginalName == srcAction.name);
            if (action == null) {
                action = new EfxActionNode() {
                    Name = srcAction.name ?? ("Action_" + file.Actions.IndexOf(srcAction)),
                };
                target.AddUniqueNamedChild(action);
                action.Owner = target.Owner ?? target;
            }
            action.index = srcAction.actionUnkn0;
            action.OriginalName = srcAction.name;
            action.LockNode(true);

            foreach (var srcAttr in srcAction.Attributes) {
                ImportAttribute(file, target, action, srcAttr);
            }

            action.RegenerateNodeName();
        }

        foreach (var bone in file.Bones) {
            resource.BoneValues ??= new();
            resource.BoneValues.Add(bone.name, bone.value);
        }

        foreach (var srcExpr in file.ExpressionParameters) {
            target.ExpressionParameters ??= new();
            var expr = new EfxExpressionParameter()
            {
                originalName = srcExpr.name,
                unknown1 = srcExpr.unkn1,
                unknown2 = srcExpr.unkn2,
                unknown3 = srcExpr.unkn3,
                unknown4 = srcExpr.unkn4,
            };
            target.ExpressionParameters.Add(expr);
        }
        foreach (var param in file.FieldParameterValues) {
            target.FieldParameterValues ??= new();
            target.FieldParameterValues.Add(new EfxFieldParameter() {
                unkn1 = param.unkn0,
                unkn2 = param.unkn2,
                type = param.type,
                unkn4 = param.unkn4,
                unkn5 = param.value_ukn1,
                unkn6 = param.value_ukn2,
                unkn7 = param.value_ukn3,
                unkn8 = param.value_ukn4,
                unkn9 = param.value_ukn5,
                unkn10 = param.value_ukn6,
                name = param.name,
                filePath = param.filePath,
            });
        }

        foreach (var param in file.CollisionEffects) {
            target.CollisionEffects ??= new();
            target.CollisionEffects.Add(new () {
                Values = new Godot.Collections.Array<uint>(param.efxEntryIndexes ?? Array.Empty<uint>()),
                OriginalName = param.conditionalEffectGroupName,
            });
        }

        return true;
    }

    private void ImportAttribute(EfxFile file, EfxRootNode target, Node parent, EFXAttribute srcAttr)
    {
        var srcfullname = srcAttr.GetType().FullName!;
        var cacheInfo = TypeCache.GetEfxStructInfo(target.Version, srcfullname);
        var attr = parent.FindChild(cacheInfo.Info.Name) as EfxAttributeNode;
        if (attr == null) {
            attr = new EfxAttributeNode() {
                Name = cacheInfo.Info.Name,
            };
            parent.AddChild(attr);
            attr.Owner = target.Owner ?? target;
        }
        attr.Type = srcAttr.type;
        attr.LockNode(true);
        attr.NodePosition = srcAttr.NodePosition.ToGodot();
        if (attr.Data == null || attr.Data.Classname != cacheInfo.Info.Classname) {
            attr.Data = new EfxObject(target.Version, cacheInfo);
        }

        AssignObject(attr.Data, srcAttr, cacheInfo);

        if (srcAttr is EFXAttributePlayEmitter emitter && emitter.efxrData != null) {
            var childRoot = attr.FindChild("Effect");
            if (childRoot == null || childRoot is not EfxRootNode childEfx) {
                childEfx = new EfxRootNode() {
                    Game = target.Game,
                    Version = target.Version,
                    Name = "Effect",
                };
                if (childRoot != null) childRoot.Name = "__" + childRoot.Name;
                attr.AddChild(childEfx);
                childEfx.Owner = target;
            }
            ImportSync(emitter.efxrData, childEfx);
        }
    }

    private EfxObject AssignObject(EfxObject target, object source, EfxClassInfo? info = null)
    {
        info ??= target.TypeInfo;
        foreach (var f in info.FieldInfos) {
            var fieldinfo = info.Fields[f.Name];
            var fieldValue = f.GetValue(source);
            target.SetField(fieldinfo, ConvertEfxValue(fieldinfo, fieldValue, target.Version));
        }
        return target;
    }

    private Variant ConvertEfxValue(EfxFieldInfo info, object? value, EfxVersion version)
    {
        if (value == null) return new Variant();
        if (!info.IsArray) {
            return ConvertSingleEfxValue(info, value, version);
        }

        var arr = new Godot.Collections.Array();

        if (value is object[] array) {
            for (int i = 0; i < array.Length; ++i) {
                arr.Add(ConvertSingleEfxValue(info, array[i], version));
            }
        } else if (value is IList listValue) {
            for (int i = 0; i < listValue.Count; ++i) {
                arr.Add(ConvertSingleEfxValue(info, listValue[i]!, version));
            }
        } else {
            throw new Exception("Unhandled efx value type " + value.GetType().FullName);
        }

        return arr;
    }

    private Variant ConvertSingleEfxValue(EfxFieldInfo info, object value, EfxVersion version)
    {
        switch (info.FieldType) {
            case RszFieldType.Object:
            case RszFieldType.Struct:
                return AssignObject(new EfxObject(version, value?.GetType().FullName ?? info.Classname), value!);
            default:
                if (value is UndeterminedFieldType ukn) return ukn.value;
                return RszTypeConverter.FromRszValueSingleValue(info.FieldType, value, Game, info.Classname);
        }
    }

    public override Task<bool> Export(EfxRootNode source, EfxFile file)
        => Task.FromResult(ExportSync(source, file));

    private bool ExportSync(EfxRootNode source, EfxFile file)
    {
        file.Header = new EfxHeader(source.Version);
        file.Header.uknFlag = source.Flags;
        file.Header.ukn = source.UnknownNum;
        foreach (var srcEntry in source.Entries) {
            var outEntry = new EFXEntry();
            outEntry.index = srcEntry.index;
            outEntry.name = srcEntry.OriginalName;
            outEntry.entryAssignment = srcEntry.assignment;
            outEntry.Version = source.Version;

            foreach (var srcAttr in srcEntry.Attributes) {
                if (srcAttr.Data == null) continue;
                var outAttr = ExportAttribute(source, srcAttr, file);
                if (outAttr == null) return false;
                outEntry.Attributes.Add(outAttr);
            }
            file.Entries.Add(outEntry);
        }
        foreach (var srcEntry in source.Actions) {
            var outEntry = new EFXAction();
            outEntry.actionUnkn0 = srcEntry.index;
            outEntry.name = srcEntry.OriginalName;
            outEntry.Version = source.Version;

            foreach (var srcAttr in srcEntry.Attributes) {
                if (srcAttr.Data == null) continue;
                var outAttr = ExportAttribute(source, srcAttr, file);
                if (outAttr == null) return false;
                outEntry.Attributes.Add(outAttr);
            }
            file.Actions.Add(outEntry);
        }

        foreach (var srcExpr in source.ExpressionParameters) {
            file.ExpressionParameters ??= new();
            file.ExpressionParameters.Add(new EFXExpressionParameter() {
                unkn1 = srcExpr.unknown1,
                unkn2 = srcExpr.unknown2,
                unkn3 = srcExpr.unknown3,
                unkn4 = srcExpr.unknown4,
                name = srcExpr.originalName,
            });
        }

        foreach (var param in source.FieldParameterValues) {
            file.FieldParameterValues ??= new();
            file.FieldParameterValues.Add(new EFXFieldParameterValue() {
                unkn0 = param.unkn1,
                unkn2 = param.unkn2,
                type = param.type,
                unkn4 = param.unkn4,
                value_ukn1 = param.unkn5,
                value_ukn2 = param.unkn6,
                value_ukn3 = param.unkn7,
                value_ukn4 = param.unkn8,
                value_ukn5 = param.unkn9,
                value_ukn6 = param.unkn10,
                name = param.name,
                filePath = param.filePath,
            });
        }

        foreach (var param in source.CollisionEffects) {
            file.CollisionEffects ??= new();
            file.CollisionEffects.Add(new() {
                conditionalEffectGroupName = param.OriginalName,
                efxEntryIndexes = param.Values?.ToArray<uint>() ?? Array.Empty<uint>(),
            });
        }

        if (source.Resource != null) {
            var resource = source.Resource;
            if (resource.BoneValues != null) {
                foreach (var (name, value) in resource.BoneValues) {
                    file.Bones.Add(new EFXBone() {
                        name = name,
                        value = value,
                    });
                }
            }
        }

        return true;
    }

    private EFXAttribute? ExportAttribute(EfxRootNode source, EfxAttributeNode srcAttr, EfxFile file)
    {
        var outAttr = EfxAttributeTypeRemapper.Create(srcAttr.Type, source.Version);
        if (outAttr == null) {
            GD.PrintErr("Invalid attribute type " + srcAttr.Type);
            return null;
        }

        outAttr.unknSeqNum = srcAttr.NodePosition.X + (srcAttr.NodePosition.Y << 8) + (srcAttr.NodePosition.Z << 16);
        outAttr.Version = source.Version;
        outAttr.type = srcAttr.Type;
        ExportObject(outAttr, srcAttr.Data!);
        if (outAttr is EFXAttributePlayEmitter emitter && srcAttr.FindChild("Effect") is EfxRootNode childRoot) {
            emitter.efxrData = new EfxFile(file.FileHandler.WithOffset(file.FileHandler.Position));
            if (!ExportSync(childRoot, emitter.efxrData)) {
                GD.PrintErr("Failed to export nested EFX data");
                return null;
            }
        }
        return outAttr;
    }

    private void ExportObject(object target, EfxObject data)
    {
        var info = data.TypeInfo;

        foreach (var f in info.FieldInfos) {
            var fieldinfo = info.Fields[f.Name];
            var srcValue = data.GetField(fieldinfo);
            var outValue = ExportEfXValue(srcValue, fieldinfo, f.FieldType, data.Version);
            f.SetValue(target, outValue);
        }
    }

    private object? ExportEfXValue(Variant value, EfxFieldInfo info, Type targetType, EfxVersion version)
    {
        if (!info.IsArray) {
            return ExportSingleEfXValue(value, info, targetType, version);
        }

        var sourceArray = value.AsGodotArray();

        if (targetType.IsArray) {
            var elemType = targetType.GetElementType()!;
            var arr = Array.CreateInstance(elemType, sourceArray.Count);
            for (int i = 0; i < sourceArray.Count; ++i) {
                arr.SetValue(ExportSingleEfXValue(sourceArray[i], info, elemType, version), i);
            }
            return arr;
        } else if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>)) {
            var list = (IList)(Activator.CreateInstance(targetType))!;
            var elementType = targetType.GetGenericArguments()[0];
            for (int i = 0; i < sourceArray.Count; ++i) {
                list.Add(ExportSingleEfXValue(sourceArray[i], info, elementType, version));
            }
            return list;
        } else {
            throw new Exception("Unhandled efx value type " + targetType.FullName);
        }
    }

    private object? ExportSingleEfXValue(Variant value, EfxFieldInfo info, Type targetType, EfxVersion version)
    {
        switch (info.FieldType) {
            case RszFieldType.Object:
            case RszFieldType.Struct:
                var instanceType = targetType;
                if (value.VariantType == Variant.Type.Object && value.As<Resource>() is EfxObject efx && !string.IsNullOrEmpty(efx.Classname)) {
                    instanceType = typeof(EFXAttribute).Assembly.GetType(efx.Classname) ?? targetType;
                }
                var targetTypeInfo = TypeCache.GetEfxStructInfo(version, instanceType.FullName!);
                if (targetTypeInfo.HasVersionedConstructor) {
                    var inst = Activator.CreateInstance(instanceType, [version])!;
                    ExportObject(inst, value.As<EfxObject>());
                    return inst;
                } else {
                    var inst = Activator.CreateInstance(instanceType)!;
                    ExportObject(inst, value.As<EfxObject>());
                    return inst;
                }

            default:
                if (targetType == typeof(UndeterminedFieldType)) return new UndeterminedFieldType(value.AsUInt32());
                return RszTypeConverter.ToRszStruct(value, info.FieldType, false, SupportedGame.Unknown);
        }
    }
}
