using Godot;
using RszTool;

namespace ReaGE;

public class Exporter
{
    private static readonly Dictionary<REObject, RszInstance> exportedInstances = new();

    public static string? ResolveExportPath(string? basePath, string? assetPath, SupportedGame game)
    {
        if (!Path.IsPathRooted(assetPath)) {
            if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(basePath)) {
                return null;
            }

            assetPath = Path.Combine(basePath, assetPath);
        }

        var config = ReachForGodot.GetAssetConfig(game) ?? throw new Exception("Missing config for game " + game);
        return PathUtils.AppendFileVersion(assetPath, config);
    }

    public static bool Export(IExportableAsset resource, string exportBasepath)
    {
        var outputPath = ResolveExportPath(exportBasepath, resource.Asset!.AssetFilename, resource.Game);
        if (string.IsNullOrEmpty(outputPath)) {
            GD.PrintErr("Invalid empty export filepath");
            return false;
        }

        var config = ReachForGodot.GetAssetConfig(resource.Game);

        Directory.CreateDirectory(outputPath.GetBaseDir());
        exportedInstances.Clear();
        if (resource is REResource reres) {
            switch (reres.ResourceType) {
                case RESupportedFileFormats.Userdata:
                    return ExportUserdata((UserdataResource)reres, outputPath, config);
                case RESupportedFileFormats.Rcol:
                    return ExportRcol(((RcolResource)reres).Instantiate(), outputPath, config);
                case RESupportedFileFormats.Foliage:
                    return ExportFoliage((FoliageResource)reres, outputPath, config);
                default:
                    GD.PrintErr("Currently unsupported export for resource type " + reres.ResourceType);
                    break;
            }
        } else if (resource is PrefabNode pfb) {
            return ExportPrefab(pfb, outputPath, config);
        } else if (resource is SceneFolder scn) {
            return ExportScene(scn, outputPath, config);
        } else if (resource is RcolRootNode rcol) {
            return ExportRcol(rcol, outputPath, config);
        } else {
            GD.PrintErr("Currently unsupported export for object type " + resource.GetType());
        }

        return false;
    }

    private static bool ExportUserdata(UserdataResource userdata, string outputFile, AssetConfig config)
    {
        var fileOption = TypeCache.CreateRszFileOptions(config);

        using var file = new UserFile(fileOption, new FileHandler(outputFile));
        SetResources(userdata.Resources, file.ResourceInfoList, fileOption);
        file.RSZ.ClearInstances();

        var rootInstance = ConstructObjectInstances(userdata.Data, file.RSZ, fileOption, file, true);
        file.RSZ.AddToObjectTable(file.RSZ.InstanceList[rootInstance]);
        return PostExport(file.Save(), outputFile);
    }

    private static bool ExportRcol(RcolRootNode? rcolRoot, string outputFile, AssetConfig config)
    {
        if (rcolRoot == null) return false;

        using var file = new RcolFile(TypeCache.CreateRszFileOptions(config), new FileHandler(outputFile));
        if (!RebuildRcol(file, rcolRoot, config)) return false;
        return PostExport(file.Save(), outputFile);
    }

    public static bool RebuildRcol(RcolFile file, RcolRootNode rcolRoot, AssetConfig config)
    {
        // var fileVersion = PathUtils.GetFileFormatVersion(RESupportedFileFormats.Rcol, config.Paths);
        // using var file = new RcolFile(TypeCache.CreateRszFileOptions(config), new FileHandler(new MemoryStream()) { FileVersion = fileVersion });

        file.RSZ.ClearInstances();

        var groupsNode = rcolRoot.FindChild("Groups");
        if (groupsNode == null) {
            GD.PrintErr("Rcol has no groups");
            return false;
        }

        file.IgnoreTags.AddRange(rcolRoot.IgnoreTags ?? Array.Empty<string>());

        var srcGroups = groupsNode.FindChildrenByType<RequestSetCollisionGroup>().ToArray();
        if (file.FileHandler.FileVersion >= 25) {
            ExportRcol25(rcolRoot, file, srcGroups);
        } else {
            ExportRcol20(rcolRoot, file, srcGroups);
        }
        return true;
    }

    private static void ExportRcol20(RcolRootNode rcolRoot, RcolFile file, RequestSetCollisionGroup[] srcGroups)
    {
        var setIndex = 0;
        Dictionary<RequestSetCollisionGroup, int> offsetCounts = new();
        foreach (var sourceSet in rcolRoot.Sets) {
            var set = new RcolFile.RequestSet();
            set.id = sourceSet.ID;
            set.name = sourceSet.OriginalName ?? string.Empty;
            set.keyName = sourceSet.KeyName ?? string.Empty;
            set.requestSetIndex = setIndex++;
            set.status = sourceSet.Status;
            if (sourceSet.Data != null) {
                var instanceId = Exporter.ConstructObjectInstances(sourceSet.Data, file.RSZ, file.Option, file, false);
                set.Userdata = file.RSZ.InstanceList[instanceId];
                file.RSZ.AddToObjectTable(set.Userdata);
                set.requestSetUserdataIndex = set.Userdata.ObjectTableIndex;
                set.groupUserdataIndexStart = set.requestSetUserdataIndex + 1;
            } else {
                set.requestSetUserdataIndex = -1;
            }
            Debug.Assert(sourceSet.Group != null);
            set.groupIndex = Array.IndexOf(srcGroups, sourceSet.Group);

            // 20-specific:
            if (!offsetCounts.TryGetValue(sourceSet.Group, out int repeatCount)) {
                offsetCounts[sourceSet.Group] = 0;
            } else {
                offsetCounts[sourceSet.Group] = ++repeatCount;
            }
            set.shapeOffset = repeatCount * sourceSet.Group.Shapes.Count();

            file.RequestSetInfoList.Add(set);
        }

        var groupIndex = 0;
        foreach (var srcGroup in srcGroups) {
            var group = srcGroup.ToRsz();
            file.GroupInfoList.Add(group);
            Debug.Assert(srcGroup.Data == null); // TODO handle this properly if we find not-null cases

            foreach (var srcShape in srcGroup.Shapes) {
                var outShape = srcShape.ToRsz(rcolRoot.Game);

                Debug.AssertIf(file.FileHandler.FileVersion < 25, !srcShape.IsExtraShape);

                foreach (var ownerSet in file.RequestSetInfoList.Where(s => s.groupIndex == groupIndex)) {
                    var srcShapeData = srcShape.SetDatas?.GetValueOrDefault(ownerSet.id);
                    if (srcShapeData == null) {
                        srcShapeData = srcShape.Data;
                    }
                    Debug.Assert(srcShapeData != null);
                    var instanceId = Exporter.ConstructObjectInstances(srcShapeData, file.RSZ, file.Option, file, false);
                    var instance = file.RSZ.InstanceList[instanceId];
                    file.RSZ.AddToObjectTable(instance);

                    if (outShape.UserData == null) {
                        outShape.UserData = file.RSZ.InstanceList[instanceId];
                        outShape.userDataIndex = outShape.UserData.ObjectTableIndex;
                    } else {
                        // for <rcol.20, extra shapes just exist in the object list
                    }
                    ownerSet.Group = file.GroupInfoList[ownerSet.groupIndex]; // not strictly needed just for exporting, but may as well
                }

                // fallback in case of group without request sets
                if (outShape.UserData == null && srcShape.Data != null) {
                    var instanceId = ConstructObjectInstances(srcShape.Data, file.RSZ, file.Option, file, false);
                    outShape.UserData = file.RSZ.InstanceList[instanceId];
                    file.RSZ.AddToObjectTable(outShape.UserData);
                    outShape.userDataIndex = outShape.UserData.ObjectTableIndex;
                }

                group.Shapes.Add(outShape);
            }
            groupIndex++;
        }
    }

    private static void ExportRcol25(RcolRootNode rcolRoot, RcolFile file, RequestSetCollisionGroup[] srcGroups)
    {
        var groupsIndexes = new Dictionary<RequestSetCollisionGroup, int>();
        int groupIndex = 0;

        foreach (var child in srcGroups) {
            var group = child.ToRsz();
            file.GroupInfoList.Add(group);
            if (child.Data != null) {
                group.Info.userDataIndex = Exporter.ConstructObjectInstances(child.Data, file.RSZ, file.Option, file, false);
                group.Info.UserData = file.RSZ.InstanceList[group.Info.userDataIndex];
            }

            foreach (var shape in child.Shapes) {
                var outShape = shape.ToRsz(rcolRoot.Game);

                Debug.Assert(shape.Data == null);

                if (shape.IsExtraShape) {
                    group.ExtraShapes.Add(outShape);
                    group.Info.extraShapes = group.ExtraShapes.Count;
                } else {
                    group.Shapes.Add(outShape);
                }
            }
            groupsIndexes[child] = groupIndex++;
        }

        var setIndex = 0;
        Dictionary<RequestSetCollisionGroup, int> offsetCounts = new();
        foreach (var sourceSet in rcolRoot.Sets) {
            var set = new RcolFile.RequestSet();
            set.id = sourceSet.ID;
            set.name = sourceSet.OriginalName ?? string.Empty;
            set.keyName = sourceSet.KeyName ?? string.Empty;
            set.requestSetIndex = setIndex++;
            set.status = sourceSet.Status;
            if (sourceSet.Data != null) {
                var instanceId = Exporter.ConstructObjectInstances(sourceSet.Data, file.RSZ, file.Option, file, false);
                set.Userdata = file.RSZ.InstanceList[instanceId];
                file.RSZ.AddToObjectTable(set.Userdata);
                set.requestSetUserdataIndex = set.Userdata.ObjectTableIndex;
                set.groupUserdataIndexStart = set.requestSetUserdataIndex + 1;
            } else {
                set.requestSetUserdataIndex = -1;
            }
            Debug.Assert(sourceSet.Group != null);

            set.groupIndex = groupsIndexes[sourceSet.Group];
            set.Group = file.GroupInfoList[set.groupIndex];

            foreach (var shape in sourceSet.Group.Shapes) {
                if (!shape.IsExtraShape) {
                    var shapeData = shape.SetDatas?.GetValueOrDefault(sourceSet.ID) ?? new REObject(rcolRoot.Game, "via.physics.RequestSetColliderUserData", true);
                    var userdata = ConstructObjectInstances(shapeData, file.RSZ, file.Option, file, false);
                    file.RSZ.AddToObjectTable(file.RSZ.InstanceList[userdata]);
                    set.ShapeUserdata.Add(file.RSZ.InstanceList[userdata]);
                }
            }

            // haven't seen this be actually used yet ¯\_(ツ)_/¯
            if (sourceSet.Group.Data != null) {
                var idx = ConstructObjectInstances(sourceSet.Group.Data, file.RSZ, file.Option, file, false);
                set.Group.Info.UserData = file.RSZ.InstanceList[idx];
            }

            file.RequestSetInfoList.Add(set);
        }
    }

    private static bool ExportFoliage(FoliageResource resource, string outputFile, AssetConfig config)
    {
        using var file = new FolFile(new FileHandler(outputFile));

        file.aabb = resource.Bounds.ToRsz();
        file.InstanceGroups = resource.Groups?.Select(grp => new FolFile.FoliageInstanceGroup() {
            transforms = grp.Transforms?.Select(tr => tr.ToRszTransform()).ToArray(),
            aabb = grp.Bounds.ToRsz(),
            materialPath = grp.Material?.Asset?.AssetFilename,
            meshPath = grp.Mesh?.Asset?.AssetFilename,
        }).ToList();

        return PostExport(file.Save(), outputFile);
    }

    private static bool ExportScene(SceneFolder root, string outputFile, AssetConfig config)
    {
        var fileOption = TypeCache.CreateRszFileOptions(config);

        root.PreExport();

        using var file = new ScnFile(fileOption, new FileHandler(outputFile));
        SetResources(root.Resources, file.ResourceInfoList, fileOption);
        file.RSZ.ClearInstances();

        foreach (var go in root.ChildObjects) {
            if (go is PrefabNode pfbGo) {
                file.PrefabInfoList.Add(new ScnFile.PrefabInfo() {
                    Path = pfbGo.Asset?.AssetFilename ?? pfbGo.Prefab,
                    parentId = 0,
                });
            }

            AddGameObject(go, file.RSZ, file, fileOption, -1);
        }

        foreach (var folder in root.Subfolders) {
            AddFolder(folder, file, fileOption, -1);
        }

        SetupScnGameObjectReferences(root, root);

        return PostExport(file.Save(), outputFile);
    }

    private static bool ExportPrefab(PrefabNode root, string outputFile, AssetConfig config)
    {
        var fileOption = TypeCache.CreateRszFileOptions(config);

        foreach (var go in root.Children) {
            go.PreExport();
        }

        using var file = new PfbFile(fileOption, new FileHandler(outputFile));
        SetResources(root.Resources, file.ResourceInfoList, fileOption);
        file.RSZ.ClearInstances();

        AddGameObject(root, file.RSZ, file, fileOption, -1);
        SetupPfbGameObjectReferences(file, root, root);
        SetupGameObjectReferenceGuids(root, root);
        return PostExport(file.Save(), outputFile);
    }

    private static void AddFolder(SceneFolder folder, ScnFile file, RszFileOption fileOption, int parentFolderId)
    {
        var folderCls = file.RszParser.GetRSZClass("via.Folder") ?? throw new Exception("Could not get folder rsz class");
        var instanceId = file.RSZ.InstanceInfoList.Count;
        var folderInstance = new RszInstance(folderCls, instanceId);

        file.RSZ.InstanceInfoList.Add(new InstanceInfo() { typeId = folderCls.typeId, CRC = folderCls.crc, ClassName = "via.Folder" });
        file.RSZ.InstanceList.Add(folderInstance);
        file.RSZ.AddToObjectTable(folderInstance);

        file.FolderInfoList.Add(new StructModel<ScnFile.FolderInfo>() { Data = new ScnFile.FolderInfo() {
            objectId = folderInstance.ObjectTableIndex,
            parentId = parentFolderId,
        } });

        var linkedSceneFilepath = folder.IsIndependentFolder && folder.Asset != null ? folder.Asset.AssetFilename : string.Empty;
        folderInstance.Values[0] = !string.IsNullOrEmpty(folder.OriginalName) ? folder.OriginalName : folder.Name.ToString();
        folderInstance.Values[1] = folder.Tag ?? string.Empty;
        folderInstance.Values[2] = folder.Update ? (byte)1 : (byte)0;
        folderInstance.Values[3] = folder.Draw ? (byte)1 : (byte)0;
        folderInstance.Values[4] = folder.Active ? (byte)1 : (byte)0;
        folderInstance.Values[5] = linkedSceneFilepath;
        if (folderInstance.Values.Length > 6) {
            folderInstance.Values[6] = (folder.Data != null && folder.Data.Length > 0) ? folder.Data : new byte[24];
        }

        if (string.IsNullOrEmpty(linkedSceneFilepath)) {
            foreach (var go in folder.ChildObjects) {
                AddGameObject(go, file.RSZ, file, fileOption, folderInstance.ObjectTableIndex);
            }

            foreach (var sub in folder.Subfolders) {
                AddFolder(sub, file, fileOption, folderInstance.ObjectTableIndex);
            }
        }
    }

    private static int AddGameObject(GameObject obj, RSZFile rsz, BaseRszFile container, RszFileOption fileOption, int parentObjectId)
    {
        var instanceId = ConstructObjectInstances(obj.Data!, rsz, fileOption, container);
        var instance = rsz.InstanceList[instanceId];

        rsz.AddToObjectTable(instance);
        if (container is ScnFile scn) {
            AddScnGameObject(instance.ObjectTableIndex, scn, obj, parentObjectId);
        } else if (container is PfbFile pfb) {
            AddPfbGameObject(instance.ObjectTableIndex, pfb, obj.Components.Count, parentObjectId);
        }

        foreach (var comp in obj.Components) {
            var typeinfo = comp.TypeInfo;
            int dataIndex = ConstructObjectInstances(comp, rsz, fileOption, container);
            rsz.AddToObjectTable(rsz.InstanceList[dataIndex]);
        }

        foreach (var child in obj.Children) {
            AddGameObject(child, rsz, container, fileOption, instance.ObjectTableIndex);
        }

        return instanceId;
    }

    private static void SetupPfbGameObjectReferences(PfbFile pfb, GameObject gameobj, PrefabNode root)
    {
        foreach (var comp in gameobj.Components) {
            RecurseSetupPfbGameObjectReferences(pfb, comp, comp, root);
        }

        foreach (var child in gameobj.Children) {
            SetupPfbGameObjectReferences(pfb, child, root);
        }
    }

    private static void SetupScnGameObjectReferences(SceneFolder folder, SceneFolder root)
    {
        foreach (var gameobj in folder.ChildObjects) {
            SetupGameObjectReferenceGuids(gameobj, root);
        }

        foreach (var sub in folder.Subfolders) {
            if (sub is not SceneFolderProxy && string.IsNullOrEmpty(sub.SceneFilePath)) {
                SetupScnGameObjectReferences(sub, root);
            }
        }
    }

    private static void SetupGameObjectReferenceGuids(GameObject gameobj, Node root)
    {
        foreach (var comp in gameobj.Components) {
            RecurseSetupGameObjectReferenceGuids(comp, comp, root);
        }

        foreach (var child in gameobj.Children) {
            SetupGameObjectReferenceGuids(child, root);
        }
    }

    private static void RecurseSetupGameObjectReferenceGuids(REObject data, REComponent component, Node root)
    {
        foreach (var field in data.TypeInfo.Fields) {
            if (field.RszField.type == RszFieldType.GameObjectRef) {
                if (!exportedInstances.TryGetValue(data, out var dataInst)) {
                    GD.PrintErr($"Could not resolve GameObjectRef source instance for field {field.SerializedName} in {component.Path}");
                    continue;
                }

                if (data.TryGetFieldValue(field, out var value)) {
                    if (field.RszField.array) {
                        var refs = value.AsGodotArray<GameObjectRef>();
                        var values = new object[refs.Count];
                        int i = 0;
                        foreach (var path in refs) {
                            values[i++] = path.ResolveGuid(component.GameObject);
                        }
                        dataInst.Values[field.FieldIndex] = values;
                    } else {
                        if (value.As<GameObjectRef>() is GameObjectRef goref && !goref.IsEmpty) {
                            dataInst.Values[field.FieldIndex] = goref.ResolveGuid(component.GameObject);
                        }
                    }
                }
            } else if (field.RszField.type == RszFieldType.Object) {
                if (data.TryGetFieldValue(field, out var child)) {
                    if (field.RszField.array) {
                        if (child.AsGodotArray<REObject>() is Godot.Collections.Array<REObject> children) {
                            foreach (var childObj in children) {
                                if (childObj != null) {
                                    RecurseSetupGameObjectReferenceGuids(childObj, component, root);
                                }
                            }
                        }
                    } else {
                        if (child.VariantType != Variant.Type.Nil && child.As<REObject>() is REObject childObj) {
                            RecurseSetupGameObjectReferenceGuids(childObj, component, root);
                        }
                    }
                }
            }
        }
    }

    private static void RecurseSetupPfbGameObjectReferences(PfbFile pfb, REObject data, REComponent component, PrefabNode root, int arrayIndex = 0)
    {
        Dictionary<string, PrefabGameObjectRefProperty>? propInfoDict = null;
        foreach (var field in data.TypeInfo.Fields) {
            if (field.RszField.type == RszFieldType.GameObjectRef) {
                if (field.RszField.array) {
                    GD.PrintErr("GameObjectRef array export currently unsupported!! " + component.Path);
                } else {
                    if (data.TryGetFieldValue(field, out var path) && path.As<GameObjectRef>() is GameObjectRef objref && !objref.IsEmpty) {
                        var target = objref.Resolve(component.GameObject);
                        if (target == null) {
                            GD.Print("Invalid pfb node path reference " + objref + " at " + component.Path);
                            continue;
                        }

                        propInfoDict ??= TypeCache.GetClassInfo(root.Game, data.Classname!).PfbRefs;
                        if (!propInfoDict.TryGetValue(field.SerializedName, out var propInfo)) {
                            GD.PrintErr("Found undeclared GameObjectRef property " + field.SerializedName);
                            continue;
                        }

                        if (!exportedInstances.TryGetValue(data, out var dataInst) || !exportedInstances.TryGetValue(target.Data!, out var targetInst)) {
                            GD.PrintErr("Could not resolve GameObjectRef instances");
                            continue;
                        }

                        // if (propInfo.AddToObjectTable && dataInst.ObjectTableIndex == -1) {
                        if (dataInst.ObjectTableIndex == -1) {
                            pfb.RSZ!.AddToObjectTable(dataInst);
                        }

                        var refEntry = new StructModel<PfbFile.GameObjectRefInfo>() {
                            Data = new PfbFile.GameObjectRefInfo() {
                                objectId = (uint)dataInst.ObjectTableIndex,
                                arrayIndex = arrayIndex,
                                propertyId = propInfo.PropertyId,
                                targetId = (uint)targetInst.ObjectTableIndex,
                            }
                        };

                        // propertyId seems to be some 16bit + 16bit value
                        // first 2 bytes would seem like a field index, but I'm not finding direct correlation between the fields and indexes
                        // the second 2 bytes seem to be a property type
                        // judging from ch000000_00 pfb, type 2 = "Exported ref" (source objectId instance added to the object info list)
                        // type 4 = something else (default?)
                        // could be some flag thing
                        pfb.GameObjectRefInfoList.Add(refEntry);
                    }
                }
            } else if (field.RszField.type == RszFieldType.Object) {
                if (data.TryGetFieldValue(field, out var child)) {
                    if (field.RszField.array) {
                        if (child.AsGodotArray<REObject>() is Godot.Collections.Array<REObject> children) {
                            int i = 0;
                            foreach (var childObj in children) {
                                if (childObj != null) {
                                    RecurseSetupPfbGameObjectReferences(pfb, childObj, component, root, i++);
                                }
                            }
                        }
                    } else {
                        if (child.VariantType != Variant.Type.Nil && child.As<REObject>() is REObject childObj) {
                            RecurseSetupPfbGameObjectReferences(pfb, childObj, component, root);
                        }
                    }
                }
            }
        }
    }

    private static void AddPfbGameObject(int objectId, PfbFile file, int componentCount, int parentId)
    {
        file.GameObjectInfoList.Add(new StructModel<PfbFile.GameObjectInfo>() {
            Data = new PfbFile.GameObjectInfo() {
                objectId = objectId,
                parentId = parentId,
                componentCount = componentCount,
            }
        });
    }

    private static void AddScnGameObject(int objectId, ScnFile file, GameObject gameObject, int parentId)
    {
        var pfbIndex = -1;
        if (gameObject is PrefabNode pfbNode && pfbNode.Asset?.IsEmpty == false) {
            pfbIndex = file.PrefabInfoList.FindIndex(pfb => pfb.Path == pfbNode.Asset.AssetFilename);
            if (pfbIndex == -1) {
                file.PrefabInfoList.Add(new ScnFile.PrefabInfo() {
                    parentId = 0,
                    Path = pfbNode.Asset.AssetFilename,
                });
            }
        }

        file.GameObjectInfoList.Add(new StructModel<ScnFile.GameObjectInfo>() {
            Data = new ScnFile.GameObjectInfo() {
                objectId = objectId,
                parentId = parentId,
                componentCount = (short)gameObject.Components.Count,
                guid = gameObject.ObjectGuid,
                prefabId = pfbIndex,
            }
        });
    }

    private static void SetResources(REResource[]? resources, List<ResourceInfo> list, RszFileOption fileOption)
    {
        if (resources != null) {
            foreach (var res in resources) {
                list.Add(new ResourceInfo(fileOption.Version) { Path = res.Asset?.AssetFilename });
            }
        }
    }

    private static int ConstructUserdataInstance(UserdataResource userdata, RSZFile rsz, RszFileOption fileOption, BaseRszFile container, bool isRoot = false)
    {
        if (exportedInstances.TryGetValue(userdata.Data, out var instance)) {
            return instance.Index;
        }
        RszClass rszClass;
        rszClass = userdata.Data.TypeInfo.RszClass;
        if (string.IsNullOrEmpty(userdata.Classname)) {
            userdata.Reimport();
            if (string.IsNullOrEmpty(userdata.Classname)) {
                throw new ArgumentNullException("Missing root userdata classname " + userdata.Classname);
            }
        }
        var path = userdata.Asset!.AssetFilename;
        RSZUserDataInfo? userDataInfo = rsz.RSZUserDataInfoList.FirstOrDefault(u => (u as RSZUserDataInfo)?.Path == path) as RSZUserDataInfo;
        if (userDataInfo == null) {
            var fileUserdataList = (container as PfbFile)?.UserdataInfoList ?? (container as ScnFile)?.UserdataInfoList ?? (container as UserFile)?.UserdataInfoList;
            fileUserdataList!.Add(new UserdataInfo() { CRC = rszClass.crc, typeId = rszClass.typeId, Path = path });
            userDataInfo = new RSZUserDataInfo() { typeId = rszClass.typeId, Path = path, instanceId = rsz.InstanceList.Count };
            rsz.RSZUserDataInfoList.Add(userDataInfo);
        }

        instance = new RszInstance(rszClass, userDataInfo.instanceId, userDataInfo, []);
        instance.Index = rsz.InstanceList.Count;
        rsz.InstanceInfoList.Add(new InstanceInfo() { ClassName = rszClass.name, CRC = rszClass.crc, typeId = rszClass.typeId });
        rsz.InstanceList.Add(instance);
        exportedInstances[userdata.Data] = instance;
        return instance.Index;
    }

    private static int ConstructObjectInstances(REObject target, RSZFile rsz, RszFileOption fileOption, BaseRszFile container, bool isRoot = false)
    {
        if (exportedInstances.TryGetValue(target, out var instance)) {
            return instance.Index;
        }
        int i = 0;
        RszClass rszClass;
        if (string.IsNullOrEmpty(target.Classname)) {
            throw new ArgumentNullException("Missing root REObject classname " + target.Classname);
        }
        rszClass = target.TypeInfo.RszClass;
        instance = new RszInstance(rszClass, rsz.InstanceList.Count, null, new object[target.TypeInfo.Fields.Length]);

        var values = instance.Values;
        foreach (var field in target.TypeInfo.Fields) {
            if (!target.TryGetFieldValue(field, out var value)) {
                values[i++] = RszInstance.CreateNormalObject(field.RszField);
                continue;
            }

            switch (field.RszField.type) {
                case RszFieldType.Object:
                    if (field.RszField.array) {
                        var array = value.AsGodotArray<REObject>() ?? throw new Exception("Unhandled rsz object array type");
                        var array_refs = new object[array.Count];
                        for (int arr_idx = 0; arr_idx < array.Count; ++arr_idx) {
                            var val = array[arr_idx];
                            array_refs[arr_idx] = val == null ? 0 : (object)ConstructObjectInstances(array[arr_idx], rsz, fileOption, container);
                        }
                        values[i++] = array_refs;
                    } else if (value.VariantType == Variant.Type.Nil) {
                        values[i++] = 0; // index 0 is the null instance entry
                    } else {
                        var obj = value.As<REObject>() ?? throw new Exception("Unhandled rsz object array type");
                        values[i++] = ConstructObjectInstances(obj, rsz, fileOption, container);
                    }
                    break;
                case RszFieldType.UserData:
                    if (field.RszField.array) {
                        var array = value.AsGodotArray<UserdataResource>() ?? throw new Exception("Unhandled rsz object array type");
                        var array_refs = new object[array.Count];
                        for (int arr_idx = 0; arr_idx < array.Count; ++arr_idx) {
                            var val = array[arr_idx];
                            array_refs[arr_idx] = val == null ? 0 : (object)ConstructUserdataInstance(array[arr_idx], rsz, fileOption, container);
                        }
                        values[i++] = array_refs;
                    } else if (value.VariantType == Variant.Type.Nil) {
                        values[i++] = 0; // index 0 is the null instance entry
                    } else {
                        var obj = value.As<UserdataResource>() ?? throw new Exception("Unhandled rsz object array type");
                        values[i++] = ConstructUserdataInstance(obj, rsz, fileOption, container);
                    }
                    break;
                case RszFieldType.Resource:
                    if (field.RszField.array) {
                        values[i++] = value.AsGodotArray<REResource>().Select(obj => obj?.Asset?.AssetFilename ?? string.Empty).ToArray();
                    } else {
                        values[i++] = value.As<REResource?>()?.Asset?.AssetFilename ?? string.Empty;
                    }
                    break;
                case RszFieldType.GameObjectRef:
                    if (field.RszField.array) {
                        values[i++] = value.AsGodotArray<GameObjectRef>().Select(p => (object)p.TargetGuid).ToArray();
                    } else {
                        values[i++] = value.As<GameObjectRef>().TargetGuid;
                    }
                    break;
                default:
                    var converted = RszTypeConverter.ToRszStruct(value, field, target.Game);
                    values[i++] = converted ?? RszInstance.CreateNormalObject(field.RszField);
                    break;
            }
        }

        instance.Index = rsz.InstanceList.Count;
        rsz.InstanceInfoList.Add(new InstanceInfo() { ClassName = rszClass.name, CRC = rszClass.crc, typeId = rszClass.typeId });
        rsz.InstanceList.Add(instance);
        exportedInstances[target] = instance;
        return instance.Index;
    }

    private static bool PostExport(bool success, string outputFile)
    {
        if (!success && File.Exists(outputFile) && new FileInfo(outputFile).Length == 0) {
            File.Delete(outputFile);
        }

        return success;
    }
}