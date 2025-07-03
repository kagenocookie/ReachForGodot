namespace ReaGE;

using System;
using System.Threading.Tasks;
using Godot;
using ReeLib;
using ReeLib.Common;
using ReeLib.Rcol;

public class RcolConverter : SceneRszAssetConverter<RcolResource, RcolFile, RcolRootNode>
{
    public override RcolFile CreateFile(FileHandler fileHandler)
    {
        return new RcolFile(Convert.FileOption, fileHandler);
    }

    public override Task<bool> Import(RcolFile file, RcolRootNode target)
    {
        var groupsNode = target.FindChild("Groups");
        if (groupsNode == null) {
            target.AddChild(groupsNode = new Node3D() { Name = "Groups" });
            groupsNode.Owner = target;
        }

        target.IgnoreTags = file.IgnoreTags?.Select(t => t.ignoreTagString).ToArray();
        var fileVersion = file.FileHandler.FileVersion;

        var groupsDict = new Dictionary<Guid, RequestSetCollisionGroup>();
        foreach (var child in groupsNode.FindChildrenByType<RequestSetCollisionGroup>()) {
            groupsDict[child.Guid] = child;
        }

        var setsDict = new Dictionary<uint, RequestSetCollider>();
        foreach (var child in target.FindChildrenByType<RequestSetCollider>()) {
            setsDict[child.ID] = child;
        }

        foreach (var srcGroup in file.Groups) {
            if (!groupsDict.TryGetValue(srcGroup.Info.guid, out var group)) {
                groupsNode.AddChild(groupsDict[srcGroup.Info.guid] = group = new RequestSetCollisionGroup());
                group.SetDisplayFolded(true);
                group.Owner = target;
                group.Guid = srcGroup.Info.guid;
            }

            group.Name = !string.IsNullOrEmpty(srcGroup.Info.Name) ? srcGroup.Info.Name : (group.Name ?? srcGroup.Info.guid.ToString());
            group.CollisionMask = srcGroup.Info.MaskBits;
            group.CollisionLayer = (uint)(1 << srcGroup.Info.LayerIndex);
            group.MaskGuids = srcGroup.Info.MaskGuids?.Select(c => c.ToString()).ToArray();
            group.LayerGuid = srcGroup.Info.LayerGuid;
            group.Data = srcGroup.Info.UserData == null ? null : CreateOrGetObject(srcGroup.Info.UserData);

            group.QueueFreeRemoveChildren();
            foreach (var srcShape in srcGroup.Shapes) {
                var shapeNode = new RequestSetCollisionShape3D();
                shapeNode.Guid = srcShape.Info.Guid;
                shapeNode.Name = !string.IsNullOrEmpty(srcShape.Info.Name) ? srcShape.Info.Name : shapeNode.Uuid!;
                shapeNode.OriginalName = srcShape.Info.Name;
                shapeNode.PrimaryJointNameStr = srcShape.Info.primaryJointNameStr;
                shapeNode.SecondaryJointNameStr = srcShape.Info.secondaryJointNameStr;
                shapeNode.LayerIndex = srcShape.Info.LayerIndex;
                shapeNode.SkipIdBits = srcShape.Info.SkipIdBits;
                shapeNode.IgnoreTagBits = srcShape.Info.IgnoreTagBits;
                shapeNode.Attribute = srcShape.Info.Attribute;
                shapeNode.Data = srcShape.Instance == null ? null : CreateOrGetObject(srcShape.Instance);
                shapeNode.RcolShapeType = srcShape.Info.shapeType;
                if (srcShape.shape != null) {
                    var fieldType = RequestSetCollisionShape3D.GetShapeFieldType(srcShape.Info.shapeType);
                    RequestSetCollisionShape3D.ApplyShape(shapeNode, srcShape.Info.shapeType, RszTypeConverter.FromRszValueSingleValue(fieldType, srcShape.shape, Game, null));
                }
                group.AddUniqueNamedChild(shapeNode);
                shapeNode.Owner = target;
            }

            foreach (var srcShape in srcGroup.ExtraShapes) {
                var shapeNode = new RequestSetCollisionShape3D();
                shapeNode.Guid = srcShape.Info.Guid;
                shapeNode.Name = "EXTRA__" + (!string.IsNullOrEmpty(srcShape.Info.Name) ? srcShape.Info.Name : shapeNode.Uuid!);
                shapeNode.IsExtraShape = true;
                shapeNode.OriginalName = srcShape.Info.Name;
                shapeNode.PrimaryJointNameStr = srcShape.Info.primaryJointNameStr;
                shapeNode.SecondaryJointNameStr = srcShape.Info.secondaryJointNameStr;
                shapeNode.LayerIndex = srcShape.Info.LayerIndex;
                shapeNode.SkipIdBits = srcShape.Info.SkipIdBits;
                shapeNode.IgnoreTagBits = srcShape.Info.IgnoreTagBits;
                shapeNode.Attribute = srcShape.Info.Attribute;
                shapeNode.Data = srcShape.Instance == null ? null : CreateOrGetObject(srcShape.Instance);
                shapeNode.RcolShapeType = srcShape.Info.shapeType;
                if (srcShape.shape != null) {
                    var fieldType = RequestSetCollisionShape3D.GetShapeFieldType(srcShape.Info.shapeType);
                    RequestSetCollisionShape3D.ApplyShape(shapeNode, srcShape.Info.shapeType, RszTypeConverter.FromRszValueSingleValue(fieldType, srcShape.shape, Game, null));
                }
                group.AddUniqueNamedChild(shapeNode);
                shapeNode.Owner = target;
            }
        }

        foreach (var importSet in file.RequestSets) {
            var name = "Set_" + importSet.Info.ID.ToString("000000") + (!string.IsNullOrEmpty(importSet.Info.Name) ? $"_{importSet.Info.Name}" : "");
            if (!setsDict.TryGetValue(importSet.Info.ID, out var requestSet)) {
                setsDict[importSet.Info.ID] = requestSet = new RequestSetCollider() { Name = name };
                target.AddUniqueNamedChild(requestSet);
                requestSet.Owner = target;
                requestSet.ID = importSet.Info.ID;
                requestSet.OriginalName = importSet.Info.Name;
                requestSet.KeyName = importSet.Info.KeyName;
            } else {
                requestSet.Name = name;
            }
            requestSet.Status = importSet.Info.status;
            if (importSet.Group != null && groupsDict.TryGetValue(importSet.Group.Info.guid, out var group)) {
                requestSet.Group = group;
                int i = 0, extra = 0;
                foreach (var shape in group.Shapes) {
                    var setData = importSet.ShapeUserdata[(shape.IsExtraShape ? extra++ : i++)];
                    shape.SetDatas ??= new();
                    shape.SetDatas[requestSet.ID] = CreateOrGetObject(setData);
                }
            }
            if (importSet.Instance != null) {
                requestSet.Data = CreateOrGetObject(importSet.Instance);
            }
        }

        return Task.FromResult(true);
    }

    public override Task<bool> Export(RcolRootNode source, RcolFile file)
    {
        if (source == null) return Task.FromResult(false);

        if (!RebuildRcol(file, source)) return Task.FromResult(false);

        return Task.FromResult(true);
    }

    public bool RebuildRcol(RcolFile file, RcolRootNode rcolRoot)
    {
        file.RSZ.ClearInstances();

        var groupsNode = rcolRoot.FindChild("Groups");
        if (groupsNode == null) {
            GD.PrintErr("Rcol has no groups");
            return false;
        }

        file.IgnoreTags.AddRange(rcolRoot.IgnoreTags?.Select(str => new IgnoreTag() { ignoreTagString = str, hash = MurMur3HashUtils.GetHash(str) }) ?? Array.Empty<IgnoreTag>());

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
            var set = new ReeLib.Rcol.RequestSet();
            set.Info.ID = sourceSet.ID;
            set.Info.Name = sourceSet.OriginalName ?? string.Empty;
            set.Info.NameHash = MurMur3HashUtils.GetHash(set.Info.Name);
            set.Info.KeyName = sourceSet.KeyName ?? string.Empty;
            set.Info.KeyHash = MurMur3HashUtils.GetHash(set.Info.Name);
            set.Info.requestSetIndex = setIndex++;
            set.Info.status = sourceSet.Status;
            if (sourceSet.Data != null) {
                set.Instance = ExportREObject(sourceSet.Data, file.RSZ, file.Option, file);
                file.RSZ.AddToObjectTable(set.Instance);
                set.Info.requestSetUserdataIndex = set.Instance.ObjectTableIndex;
                set.Info.groupUserdataIndexStart = set.Info.requestSetUserdataIndex + 1;
            } else {
                set.Info.requestSetUserdataIndex = -1;
            }
            Debug.Assert(sourceSet.Group != null);
            set.Info.GroupIndex = Array.IndexOf(srcGroups, sourceSet.Group);

            // 20-specific:
            if (!offsetCounts.TryGetValue(sourceSet.Group, out int repeatCount)) {
                offsetCounts[sourceSet.Group] = 0;
            } else {
                offsetCounts[sourceSet.Group] = ++repeatCount;
            }
            set.Info.ShapeOffset = repeatCount * sourceSet.Group.Shapes.Count();

            file.RequestSets.Add(set);
        }

        var groupIndex = 0;
        foreach (var srcGroup in srcGroups) {
            var group = srcGroup.ToRsz();
            file.Groups.Add(group);
            Debug.Assert(srcGroup.Data == null); // TODO handle this properly if we find not-null cases

            foreach (var srcShape in srcGroup.Shapes) {
                var outShape = srcShape.ToRsz(rcolRoot.Game);

                Debug.AssertIf(file.FileHandler.FileVersion < 25, !srcShape.IsExtraShape);

                foreach (var ownerSet in file.RequestSets.Where(s => s.Info.GroupIndex == groupIndex)) {
                    var srcShapeData = srcShape.SetDatas?.GetValueOrDefault(ownerSet.Info.ID);
                    if (srcShapeData == null) {
                        srcShapeData = srcShape.Data;
                    }
                    Debug.Assert(srcShapeData != null);
                    var instance = ExportREObject(srcShapeData, file.RSZ, file.Option, file);
                    file.RSZ.AddToObjectTable(instance);

                    if (outShape.Instance == null) {
                        outShape.Instance = file.RSZ.InstanceList[instance.Index];
                        outShape.Info.UserDataIndex = outShape.Instance.ObjectTableIndex;
                    } else {
                        // for <rcol.20, extra shapes just exist in the object list
                    }
                    ownerSet.Group = file.Groups[ownerSet.Info.GroupIndex]; // not strictly needed just for exporting, but may as well
                }

                // fallback in case of group without request sets
                if (outShape.Instance == null && srcShape.Data != null) {
                    outShape.Instance = ExportREObject(srcShape.Data, file.RSZ, file.Option, file);
                    file.RSZ.AddToObjectTable(outShape.Instance);
                    outShape.Info.UserDataIndex = outShape.Instance.ObjectTableIndex;
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
            file.Groups.Add(group);
            if (child.Data != null) {
                group.Info.UserData = ExportREObject(child.Data, file.RSZ, file.Option, file);
                group.Info.UserDataIndex = group.Info.UserData.Index;
            }

            foreach (var shape in child.Shapes) {
                var outShape = shape.ToRsz(rcolRoot.Game);

                if (shape.IsExtraShape) {
                    group.Info.UserDataIndex = 1;
                    group.ExtraShapes.Add(outShape);
                    group.Info.NumExtraShapes = group.ExtraShapes.Count;
                } else {
                    group.Info.UserDataIndex = 0;
                    group.Shapes.Add(outShape);
                }
            }
            groupsIndexes[child] = groupIndex++;
        }

        var setIndex = 0;
        Dictionary<RequestSetCollisionGroup, int> offsetCounts = new();
        foreach (var sourceSet in rcolRoot.Sets) {
            var set = new ReeLib.Rcol.RequestSet();
            set.Info.ID = sourceSet.ID;
            set.Info.Name = sourceSet.OriginalName ?? string.Empty;
            set.Info.NameHash = MurMur3HashUtils.GetHash(set.Info.Name);
            set.Info.KeyName = sourceSet.KeyName ?? string.Empty;
            set.Info.KeyHash = MurMur3HashUtils.GetHash(set.Info.Name);
            set.Info.requestSetIndex = setIndex++;
            set.Info.status = sourceSet.Status;
            if (sourceSet.Data != null) {
                set.Instance = ExportREObject(sourceSet.Data, file.RSZ, file.Option, file);
                file.RSZ.AddToObjectTable(set.Instance);
                set.Info.requestSetUserdataIndex = set.Instance.ObjectTableIndex;
                set.Info.groupUserdataIndexStart = set.Info.requestSetUserdataIndex + 1;
            } else {
                set.Info.requestSetUserdataIndex = -1;
            }
            Debug.Assert(sourceSet.Group != null);

            set.Info.GroupIndex = groupsIndexes[sourceSet.Group];
            set.Group = file.Groups[set.Info.GroupIndex];

            foreach (var shape in sourceSet.Group.Shapes) {
                if (!shape.IsExtraShape) {
                    var shapeData = shape.SetDatas?.GetValueOrDefault(sourceSet.ID) ?? new REObject(rcolRoot.Game, "via.physics.RequestSetColliderUserData", true);
                    var userdata = ExportREObject(shapeData, file.RSZ, file.Option, file);
                    file.RSZ.AddToObjectTable(userdata);
                    set.ShapeUserdata.Add(userdata);
                }
            }

            // haven't seen this be actually used yet ¯\_(ツ)_/¯
            if (sourceSet.Group.Data != null) {
                set.Group.Info.UserData = ExportREObject(sourceSet.Group.Data, file.RSZ, file.Option, file);
            }

            file.RequestSets.Add(set);
        }
    }

}
