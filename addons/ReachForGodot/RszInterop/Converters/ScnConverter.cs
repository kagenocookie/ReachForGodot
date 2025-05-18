namespace ReaGE;

using System;
using System.Threading.Tasks;
using Godot;
using RszTool;

public class ScnConverter : SceneRszAssetConverter<SceneResource, ScnFile, SceneFolder>
{
    public override bool LoadFile(ScnFile file)
    {
        if (!file.Read()) return false;
        file.SetupGameObjects();
        return true;
    }

    public override ScnFile CreateFile(FileHandler fileHandler)
    {
        return new ScnFile(Convert.FileOption, fileHandler);
    }

    protected override void PreCreateScenePlaceholder(SceneFolder node, SceneResource target)
    {
        node.LockNode(true);
    }

    public override Task<bool> Export(SceneFolder source, ScnFile file)
    {
        source.PreExport();

        foreach (var f in source.GetAllSubfoldersIncludingSelfOwnedBy(source)) {
            foreach (var go in f.ChildObjects) {
                foreach (var child in go.GetAllChildrenIncludingSelfOwnedBy(source)) {
                    AddMissingResourceInfos(go, source);
                }
            }
        }

        StoreResources(source.Resources, file.ResourceInfoList, false);
        file.RSZ.ClearInstances();

        foreach (var go in source.ChildObjects) {
            if (go is PrefabNode pfbGo) {
                file.PrefabInfoList.Add(new RszTool.Scn.ScnPrefabInfo() {
                    Path = pfbGo.Asset?.ExportedFilename ?? pfbGo.Prefab,
                    parentId = 0,
                });
            }

            AddGameObject(go, file.RSZ, file, -1);
        }

        foreach (var folder in source.Subfolders) {
            AddFolder(folder, file, -1);
        }

        SetupScnGameObjectReferences(source, source);
        return Task.FromResult(true);
    }

    private void SetupScnGameObjectReferences(SceneFolder folder, SceneFolder root)
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

    private void AddFolder(SceneFolder folder, ScnFile file, int parentFolderId)
    {
        var folderCls = file.RszParser.GetRSZClass("via.Folder") ?? throw new Exception("Could not get folder rsz class");
        var instanceId = file.RSZ.InstanceInfoList.Count;
        var folderInstance = new RszInstance(folderCls, instanceId);

        file.RSZ.InstanceInfoList.Add(new InstanceInfo(Convert.FileOption.Version) { typeId = folderCls.typeId, CRC = folderCls.crc, ClassName = "via.Folder" });
        file.RSZ.InstanceList.Add(folderInstance);
        file.RSZ.AddToObjectTable(folderInstance);

        file.FolderInfoList.Add(new StructModel<RszTool.Scn.ScnFolderInfo>() { Data = new RszTool.Scn.ScnFolderInfo() {
            objectId = folderInstance.ObjectTableIndex,
            parentId = parentFolderId,
        } });

        var linkedSceneFilepath = folder.IsIndependentFolder && folder.Asset != null ? folder.Asset.ExportedFilename : string.Empty;
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
                AddGameObject(go, file.RSZ, file, folderInstance.ObjectTableIndex);
            }

            foreach (var sub in folder.Subfolders) {
                AddFolder(sub, file, folderInstance.ObjectTableIndex);
            }
        }
    }

    protected RszInstance AddGameObject(GameObject obj, RSZFile rsz, ScnFile file, int parentObjectId)
    {
        var instance = ExportREObject(obj.Data!, rsz, FileOption, file);

        rsz.AddToObjectTable(instance);
        AddScnGameObject(instance.ObjectTableIndex, file, obj, parentObjectId);

        foreach (var comp in obj.Components) {
            var typeinfo = comp.TypeInfo;
            rsz.AddToObjectTable(ExportREObject(comp, rsz, FileOption, file));
        }

        foreach (var child in obj.Children) {
            AddGameObject(child, rsz, file, instance.ObjectTableIndex);
        }

        return instance;
    }

    private void AddScnGameObject(int objectId, ScnFile file, GameObject gameObject, int parentId)
    {
        var pfbIndex = -1;
        if (gameObject is PrefabNode pfbNode && !string.IsNullOrEmpty(pfbNode.Prefab)) {
            pfbIndex = file.PrefabInfoList.FindIndex(pfb => pfb.Path == pfbNode.Prefab);
            if (pfbIndex == -1) {
                pfbIndex = file.PrefabInfoList.Count;
                file.PrefabInfoList.Add(new RszTool.Scn.ScnPrefabInfo() {
                    parentId = 0,
                    Path = pfbNode.Prefab,
                });
            }
        }

        var info = new RszTool.Scn.ScnGameObjectInfo() {
            objectId = objectId,
            parentId = parentId,
            componentCount = (short)gameObject.Components.Count,
            guid = gameObject.ObjectGuid,
            prefabId = pfbIndex,
            ukn = Game.UsesEmbeddedUserdata() ? (short)-1 : (short)0, // DMC5 only?
        };
        file.GameObjectInfoList.Add(info);

        file.GameObjects ??= new();
        file.GameObjects.Add(new RszTool.Scn.ScnGameObject() {
            Info = info,
        });
    }

    public override async Task<bool> Import(ScnFile file, SceneFolder target)
    {
        TypeCache.StoreInferredRszTypes(file.RSZ, Config);

        if (Convert.Options.folders == RszImportType.ForceReimport) {
            target.Clear();
        }
        // GD.Print("Starting import for scene " + target.Asset?.AssetFilename);

        var batch = Convert.CreateFolderBatch(target, null, target.Asset?.AssetFilename);
        Convert.StartBatch(batch);
        GenerateResources(target, file.ResourceInfoList);
        PrepareFolderBatch(batch, file.GameObjects!, file.FolderDatas!);
        await AwaitFolderBatch(batch, true);
        Convert.Context.UpdateUIStatus();

        target.RecalculateBounds(true);

        if (target.Owner == null) {
            target.Update = true;
            target.Draw = true;
            target.Active = true;
            ReconstructScnFolderGameObjectRefs(file, target, target);
        }

        Convert.EndBatch(batch);

        Log(" Finished scene tree " + target.Name);
        if (WritesEnabled && !target.IsInsideTree()) {
            CreateOrReplaceRszSceneResource(target, target.Asset!);
        }

        return true;
    }

    private void ReconstructScnFolderGameObjectRefs(ScnFile file, SceneFolder folder, SceneFolder root)
    {
        foreach (var go in folder.ChildObjects.SelectMany(ch => ch.AllChildrenIncludingSelf)) {
            foreach (var comp in go.Components) {
                ReconstructScnGameObjectRefs(comp, comp, go, root);
            }
        }

        foreach (var sub in folder.Subfolders) {
            ReconstructScnFolderGameObjectRefs(file, sub, root);
        }
    }

    private void ReconstructScnGameObjectRefs(REObject obj, REComponent component, GameObject gameobj, SceneFolder root)
    {
        foreach (var field in obj.TypeInfo.Fields) {
            if (field.RszField.type == RszFieldType.GameObjectRef && obj.TryGetFieldValue(field, out var value)) {
                if (field.RszField.array) {
                    var paths = value.AsGodotArray<GameObjectRef>();
                    for (int i = 0; i < paths.Count; ++i) {
                        var path = paths[i];
                        if (gameObjects.TryGetValue(path.TargetGuid, out var refTarget) && gameobj.Owner == refTarget.Owner) {
                            path.ModifyPathNoCheck(gameobj.GetPathTo(refTarget));
                        } else {
                            // for cross-scn references, we can't guaranteed resolve them so just store the guid without a path
                            path.Path = null;
                        }
                    }
                    obj.SetField(field, paths);
                } else {
                    var path = value.As<GameObjectRef>();
                    if (gameObjects.TryGetValue(path.TargetGuid, out var refTarget) && gameobj.Owner == refTarget.Owner) {
                        path.ModifyPathNoCheck(gameobj.GetPathTo(refTarget));
                    } else {
                        // for cross-scn references, we can't guaranteed resolve them so just store the guid without a path
                        path.Path = null;
                    }
                }

            } else if (field.RszField.type is RszFieldType.Object) {
                if (!obj.TryGetFieldValue(field, out var child)) continue;

                if (field.RszField.array) {
                    if (child.AsGodotArray<REObject>() is Godot.Collections.Array<REObject> children) {
                        foreach (var childObj in children) {
                            if (childObj != null) {
                                ReconstructScnGameObjectRefs(childObj, component, gameobj, root);
                            }
                        }
                    }
                } else {
                    if (child.As<REObject>() is REObject childObj) {
                        ReconstructScnGameObjectRefs(childObj, component, gameobj, root);
                    }
                }
            }
        }
    }

    public Task RegenerateFromSourceFile(SceneFolder folder)
    {
        if (folder.Asset == null) return Task.CompletedTask;
        var source = folder.Asset.FindSourceFile(Config);
        if (string.IsNullOrEmpty(source)) {
            GD.PrintErr("Could not find referenced scene file in any known folders: " + folder.Asset.AssetFilename);
            return Task.CompletedTask;
        }
        using var file = CreateFile(source);
        LoadFile(file);
        return Import(file, folder);
    }

    private void PrepareFolderBatch(AssetConverter.FolderBatch batch, IEnumerable<RszTool.Scn.ScnGameObject> gameobjects, IEnumerable<RszTool.Scn.ScnFolderData> folders)
    {
        var dupeDict = new Dictionary<string, int>();
        foreach (var gameObj in gameobjects) {
            Debug.Assert(gameObj.Info != null);

            var childName = gameObj.Name ?? "";
            if (dupeDict.TryGetValue(childName, out var index)) {
                dupeDict[childName] = ++index;
            } else {
                dupeDict[childName] = index = 0;
            }

            var objBatch = Convert.CreateGameObjectBatch(batch.folder.Path + "/" + gameObj.Name + index);
            batch.gameObjects.Add(objBatch);
            PrepareGameObjectBatch(gameObj, objBatch, batch.folder, null, index);
        }
        dupeDict.Clear();

        foreach (var folder in folders) {
            Debug.Assert(folder.Info != null);
            var folderName = folder.Name ?? "";
            if (dupeDict.TryGetValue(folderName, out var index)) {
                dupeDict[folderName] = ++index;
            } else {
                dupeDict[folderName] = index = 0;
            }

            PrepareSubfolderPlaceholders(batch.folder, folder, batch.folder, batch, index);
        }
    }

    private void PrepareSubfolderPlaceholders(SceneFolder root, RszTool.Scn.ScnFolderData folder, SceneFolder parent, AssetConverter.FolderBatch batch, int dedupeIndex)
    {
        Debug.Assert(folder.Info != null);
        var name = !string.IsNullOrEmpty(folder.Name) ? folder.Name : "UnnamedFolder";
        var subfolder = parent.GetFolder(name, dedupeIndex);
        if (folder.Instance?.GetFieldValue("Path") is string scnPath && !string.IsNullOrWhiteSpace(scnPath)) {
            var isNew = false;
            if (subfolder == null) {
                subfolder = Importer.FindOrImportAsset<PackedScene>(scnPath, Config)?.Instantiate<SceneFolder>()
                    ?? new SceneFolder() { Name = name, OriginalName = name, Asset = new AssetReference(scnPath), Game = Game };
                subfolder.LockNode(true);
                if (parent.GetNodeOrNull(name) != null) {
                    subfolder.Name = name + "__folder";
                }
                parent.AddFolder(subfolder);
                isNew = true;
            }

            subfolder.OriginalName = name;
            Debug.Assert(!folder.Children.Any());

            var skipImport = (Convert.Options.folders == RszImportType.Placeholders || !isNew && Convert.Options.folders == RszImportType.CreateOrReuse);
            if (!skipImport) {
                (subfolder as SceneFolderProxy)?.UnloadScene();
                var newBatch = Convert.CreateFolderBatch(subfolder, folder, scnPath);
                batch.folders.Add(newBatch);
            }
        } else {
            if (subfolder == null) {
                Log("Creating folder " + name);
                subfolder = new SceneFolder() {
                    Game = root.Game,
                    Name = parent.GetNodeOrNull(name) != null ? name + "__folder" : name,
                    OriginalName = name,
                };
                subfolder.LockNode(true);
                parent.AddFolder(subfolder);
            } else {
                Debug.Assert(string.IsNullOrEmpty(subfolder.SceneFilePath));
            }

            var newBatch = Convert.CreateFolderBatch(subfolder, folder, subfolder.Path);
            batch.folders.Add(newBatch);
            PrepareFolderBatch(newBatch, folder.GameObjects, folder.Children);
        }

        subfolder.Tag = folder.Instance!.GetFieldValue("Tag") as string;
        subfolder.Update = (byte)folder.Instance!.GetFieldValue("Update")! != 0;
        subfolder.Draw = (byte)folder.Instance!.GetFieldValue("Draw")! != 0;
        subfolder.Active = (byte)folder.Instance!.GetFieldValue("Select")! != 0;
        subfolder.Data = folder.Instance!.GetFieldValue("Data") as byte[];
    }

    private async Task AwaitFolderBatch(AssetConverter.FolderBatch batch, bool isRoot)
    {
        Convert.StartBatch(batch);
        await Convert.Context.MaybeYield();

        var folder = batch.folder;
        if (!isRoot && folder.Asset?.IsEmpty == false) {
            var folderName = folder.OriginalName;
            var scene = (folder as SceneFolderProxy)?.Contents
                ?? (!string.IsNullOrEmpty(folder.SceneFilePath)
                    ? ResourceLoader.Load<PackedScene>(folder.SceneFilePath)
                    : Importer.FindOrImportAsset<PackedScene>(folder.Asset.AssetFilename, Config, WritesEnabled));
            if (scene == null) {
                // not every scene is shipped (specific case: RE2 Location/RPD/Level_199)
                // keep these as placeholders in whatever scenes contain them
                Convert.EndBatch(batch);
                return;
            }
            int nodeCount = 0;
            var newInstance = scene.Instantiate<SceneFolder>();
            if (!batch.finishedFolders.Contains(folder) && Convert.Options.linkedScenes) {
                var importPath = folder.Asset?.GetImportFilepath(Config);
                var childFullPath = folder.Asset?.FindSourceFile(Config);
                await RegenerateFromSourceFile(newInstance);
                Convert.Context.UpdateUIStatus();
                folder.KnownBounds = newInstance.KnownBounds;
                scene.Pack(newInstance);
                nodeCount = newInstance.NodeCount;

                if (importPath == null || childFullPath == null) {
                    ErrorLog("Invalid folder source file " + folder.Asset?.ToString());
                    return;
                }
            }
            batch.finishedFolders.Add(folder);
            newInstance.CopyDataFrom(folder);
            newInstance.OriginalName = folderName;
            if (folder is SceneFolderProxy proxy) {
                proxy.ShowLinkedFolder = true; // TODO configurable by import type?
            } else {
                if (nodeCount > ReachForGodot.SceneFolderProxyThreshold && WritesEnabled) {
                    batch.folder = folder.ReplaceWithProxy();
                    // tempInstance = tempInstance.ReplaceWithProxy();
                } else {
                    folder.GetParent().EmplaceChild(folder, newInstance);
                }
            }
        }

        await batch.AwaitGameObjects(Convert);
        for (var i = 0; i < batch.folders.Count; i++) {
            AssetConverter.FolderBatch subfolder = batch.folders[i];
            await AwaitFolderBatch(subfolder, false);
            batch.FinishedFolderCount++;
        }

        Convert.EndBatch(batch);
    }
}
