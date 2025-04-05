namespace ReaGE;

using System;
using System.Threading.Tasks;
using Godot;
using RszTool;

public class RcolConverter : RszAssetConverter<RcolRootNode, RcolFile, RcolResource>
{
    public override RcolResource CreateOrReplaceResourcePlaceholder(AssetReference reference)
    {
        return SetupResource(new RcolResource(), reference);
    }

    public override RcolFile CreateFile(FileHandler fileHandler)
    {
        return new RcolFile(Convert.FileOption, fileHandler);
    }

    public Task<bool> Export(RcolResource resource, RcolFile target)
    {
        return Export(resource.Instantiate() as RcolRootNode ?? new RcolRootNode(), target);
    }

    public Task<bool> ImportFromFile(RcolResource target)
    {
        var node = target.Instantiate();
        if (node == null) return Task.FromResult(false);
        return ImportFromFile(node);
    }

    public override Task<bool> Import(RcolFile file, RcolRootNode target)
    {
        var groupsNode = target.FindChild("Groups");
        if (groupsNode == null) {
            target.AddChild(groupsNode = new Node3D() { Name = "Groups" });
            groupsNode.Owner = target;
        }

        target.IgnoreTags = file.IgnoreTags.ToArray();
        var fileVersion = file.FileHandler.FileVersion;

        var groupsDict = new Dictionary<Guid, RequestSetCollisionGroup>();
        foreach (var child in groupsNode.FindChildrenByType<RequestSetCollisionGroup>()) {
            groupsDict[child.Guid] = child;
        }

        var setsDict = new Dictionary<uint, RequestSetCollider>();
        foreach (var child in target.FindChildrenByType<RequestSetCollider>()) {
            setsDict[child.ID] = child;
        }

        foreach (var srcGroup in file.GroupInfoList) {
            if (!groupsDict.TryGetValue(srcGroup.Info.guid, out var group)) {
                groupsNode.AddChild(groupsDict[srcGroup.Info.guid] = group = new RequestSetCollisionGroup());
                group.SetDisplayFolded(true);
                group.Owner = target;
                group.Guid = srcGroup.Info.guid;
            }

            group.Name = !string.IsNullOrEmpty(srcGroup.Info.name) ? srcGroup.Info.name : (group.Name ?? srcGroup.Info.guid.ToString());
            group.CollisionMask = srcGroup.Info.MaskBits;
            group.CollisionLayer = (uint)(1 << srcGroup.Info.layerIndex);
            group.MaskGuids = srcGroup.Info.MaskGuids.Select(c => c.ToString()).ToArray();
            group.LayerGuid = srcGroup.Info.layerGuid;
            group.Data = srcGroup.Info.UserData == null ? null : CreateOrGetObject(srcGroup.Info.UserData);

            group.ClearChildren();
            foreach (var srcShape in srcGroup.Shapes) {
                var shapeNode = new RequestSetCollisionShape3D();
                shapeNode.Guid = srcShape.Guid;
                shapeNode.Name = !string.IsNullOrEmpty(srcShape.Name) ? srcShape.Name : shapeNode.Uuid!;
                shapeNode.OriginalName = srcShape.Name;
                shapeNode.PrimaryJointNameStr = srcShape.PrimaryJointNameStr;
                shapeNode.SecondaryJointNameStr = srcShape.SecondaryJointNameStr;
                shapeNode.LayerIndex = srcShape.LayerIndex;
                shapeNode.SkipIdBits = srcShape.SkipIdBits;
                shapeNode.IgnoreTagBits = srcShape.IgnoreTagBits;
                shapeNode.Attribute = srcShape.Attribute;
                shapeNode.Data = srcShape.UserData == null ? null : CreateOrGetObject(srcShape.UserData);
                shapeNode.RcolShapeType = srcShape.shapeType;
                if (srcShape.shape != null) {
                    var fieldType = RequestSetCollisionShape3D.GetShapeFieldType(srcShape.shapeType);
                    RequestSetCollisionShape3D.ApplyShape(shapeNode, srcShape.shapeType, RszTypeConverter.FromRszValueSingleValue(fieldType, srcShape.shape, Game));
                }
                group.AddUniqueNamedChild(shapeNode);
                shapeNode.Owner = target;
            }

            foreach (var srcShape in srcGroup.ExtraShapes) {
                var shapeNode = new RequestSetCollisionShape3D();
                shapeNode.Guid = srcShape.Guid;
                shapeNode.Name = "EXTRA__" + (!string.IsNullOrEmpty(srcShape.Name) ? srcShape.Name : shapeNode.Uuid!);
                shapeNode.IsExtraShape = true;
                shapeNode.OriginalName = srcShape.Name;
                shapeNode.PrimaryJointNameStr = srcShape.PrimaryJointNameStr;
                shapeNode.SecondaryJointNameStr = srcShape.SecondaryJointNameStr;
                shapeNode.LayerIndex = srcShape.LayerIndex;
                shapeNode.SkipIdBits = srcShape.SkipIdBits;
                shapeNode.IgnoreTagBits = srcShape.IgnoreTagBits;
                shapeNode.Attribute = srcShape.Attribute;
                shapeNode.Data = srcShape.UserData == null ? null : CreateOrGetObject(srcShape.UserData);
                shapeNode.RcolShapeType = srcShape.shapeType;
                if (srcShape.shape != null) {
                    var fieldType = RequestSetCollisionShape3D.GetShapeFieldType(srcShape.shapeType);
                    RequestSetCollisionShape3D.ApplyShape(shapeNode, srcShape.shapeType, RszTypeConverter.FromRszValueSingleValue(fieldType, srcShape.shape, Game));
                }
                group.AddUniqueNamedChild(shapeNode);
                shapeNode.Owner = target;
            }
        }

        foreach (var importSet in file.RequestSetInfoList) {
            var name = "Set_" + importSet.id.ToString("000000") + (!string.IsNullOrEmpty(importSet.name) ? $"_{importSet.name}" : "");
            if (!setsDict.TryGetValue(importSet.id, out var requestSet)) {
                setsDict[importSet.id] = requestSet = new RequestSetCollider() { Name = name };
                target.AddUniqueNamedChild(requestSet);
                requestSet.Owner = target;
                requestSet.ID = importSet.id;
                requestSet.OriginalName = importSet.name;
                requestSet.KeyName = importSet.keyName;
            } else {
                requestSet.Name = name;
            }
            requestSet.Status = importSet.status;
            if (importSet.Group != null && groupsDict.TryGetValue(importSet.Group.Info.guid, out var group)) {
                requestSet.Group = group;
                int i = 0, extra = 0;
                foreach (var shape in group.Shapes) {
                    var setData = importSet.ShapeUserdata[(shape.IsExtraShape ? extra++ : i++)];
                    shape.SetDatas ??= new();
                    shape.SetDatas[requestSet.ID] = CreateOrGetObject(setData);
                }
                // if (fileVersion >= 25) {
                //     int i = 0, extra = 0;
                //     foreach (var shape in group.Shapes) {
                //         var setData = importSet.ShapeUserdata[(shape.IsExtraShape ? extra++ : i++)];
                //         shape.SetDatas ??= new();
                //         shape.SetDatas[requestSet] = CreateOrGetObject(setData);
                //     }
                // }
            }
            if (importSet.Userdata != null) {
                requestSet.Data = CreateOrGetObject(importSet.Userdata);
            }
        }

        return Task.FromResult(true);
    }

    public override Task<bool> Export(RcolRootNode source, RcolFile file)
    {
        if (source == null) return Task.FromResult(false);

        if (!RebuildRcol(file, source)) return Task.FromResult(false);

        return Task.FromResult(PostExport(file.Save(), file.FileHandler.FilePath!));
    }

    public bool RebuildRcol(RcolFile file, RcolRootNode rcolRoot)
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

    private void ExportRcol20(RcolRootNode rcolRoot, RcolFile file, RequestSetCollisionGroup[] srcGroups)
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
                set.Userdata = ExportREObject(sourceSet.Data, file.RSZ, file.Option, file, false);
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
                    var instance = ExportREObject(srcShapeData, file.RSZ, file.Option, file, false);
                    file.RSZ.AddToObjectTable(instance);

                    if (outShape.UserData == null) {
                        outShape.UserData = file.RSZ.InstanceList[instance.Index];
                        outShape.userDataIndex = outShape.UserData.ObjectTableIndex;
                    } else {
                        // for <rcol.20, extra shapes just exist in the object list
                    }
                    ownerSet.Group = file.GroupInfoList[ownerSet.groupIndex]; // not strictly needed just for exporting, but may as well
                }

                // fallback in case of group without request sets
                if (outShape.UserData == null && srcShape.Data != null) {
                    outShape.UserData = ExportREObject(srcShape.Data, file.RSZ, file.Option, file, false);
                    file.RSZ.AddToObjectTable(outShape.UserData);
                    outShape.userDataIndex = outShape.UserData.ObjectTableIndex;
                }

                group.Shapes.Add(outShape);
            }
            groupIndex++;
        }
    }

    private void ExportRcol25(RcolRootNode rcolRoot, RcolFile file, RequestSetCollisionGroup[] srcGroups)
    {
        var groupsIndexes = new Dictionary<RequestSetCollisionGroup, int>();
        int groupIndex = 0;

        foreach (var child in srcGroups) {
            var group = child.ToRsz();
            file.GroupInfoList.Add(group);
            if (child.Data != null) {
                group.Info.UserData = ExportREObject(child.Data, file.RSZ, file.Option, file, false);
                group.Info.userDataIndex = group.Info.UserData.Index;
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
                set.Userdata = ExportREObject(sourceSet.Data, file.RSZ, file.Option, file, false);
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
                    var userdata = ExportREObject(shapeData, file.RSZ, file.Option, file, false);
                    file.RSZ.AddToObjectTable(userdata);
                    set.ShapeUserdata.Add(userdata);
                }
            }

            // haven't seen this be actually used yet ¯\_(ツ)_/¯
            if (sourceSet.Group.Data != null) {
                set.Group.Info.UserData = ExportREObject(sourceSet.Group.Data, file.RSZ, file.Option, file, false);
            }

            file.RequestSetInfoList.Add(set);
        }
    }

}
