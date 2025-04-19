namespace ReaGE;

using System;
using Godot;
using Godot.Collections;

[GlobalClass, Tool]
public partial class ObjectTemplateRoot : Node
{
    [Export] public Dictionary<string, Dictionary<string, string>>? ResourceProperties { get; set; }
    public T GetTarget<T>() where T : GodotObject => GetChildOrNull<T>(0) as T ?? throw new Exception($"Object template root is missing a target node of expected type {typeof(T)}");

    public void ExtractGameObjectResources()
    {
        ResourceProperties ??= new();
        var root = GetChildOrNull<Node>(0);
        if (root == null) return;

        foreach (var gameobj in root.FindChildrenByTypeRecursive<GameObject>(true)) {
            var nodePath = root.GetPathTo(gameobj);
            foreach (var comp in gameobj.Components) {
                foreach (var (path, obj) in comp.GetEngineObjectsWithPaths()) {
                    if (obj is REResource res && !string.IsNullOrEmpty(res.Asset?.ExportedFilename)) {
                        if (!ResourceProperties.TryGetValue(nodePath, out var nodeDict)) {
                            ResourceProperties[nodePath] = nodeDict = new();
                        }
                        nodeDict[comp.Classname + ":" + path] = res.Asset.ExportedFilename;
                    }
                }
                comp.ClearResources();
            }
        }
    }

    public void ExtractSelfResources(string storageName)
    {
        ResourceProperties ??= new();

        var props = GetPropertyList();
        foreach (var prop in props) {
            var propName = prop["name"].AsStringName();
            var value = Get(propName);
            if (value.VariantType == Variant.Type.Object && value.As<Resource>() is REObject target) {
                foreach (var (path, obj) in target.GetEngineObjectsWithPaths()) {
                    if (obj is REResource res && !string.IsNullOrEmpty(res.Asset?.ExportedFilename)) {
                        if (!ResourceProperties.TryGetValue(storageName, out var nodeDict)) {
                            ResourceProperties[storageName] = nodeDict = new();
                        }
                        nodeDict[path] = res.Asset.ExportedFilename;
                    }
                }
                target.ClearResources();
            }
        }
    }

    public void ApplyProperties(REComponent component, string storageName)
    {
        if (ResourceProperties == null || !ResourceProperties.TryGetValue(storageName, out var props)) return;

        var config = ReachForGodot.GetAssetConfig(component.Game);

        foreach (var (path, resourcePath) in props) {
            var resource = Importer.FindOrImportResource<Resource>(resourcePath, config);
            if (resource == null) {
                GD.PrintErr("Stored template resource not found: " + path);
                continue;
            }
            component.SetFieldByPath(path, resource);
        }
    }

    public void ApplyProperties(GameObject instance)
    {
        if (ResourceProperties == null) return;

        var config = ReachForGodot.GetAssetConfig(instance.Game);

        foreach (var (nodePath, nodeDict) in ResourceProperties) {
            var gameobj = instance.GetNodeOrNull<GameObject>(nodePath);
            if (gameobj == null) {
                GD.Print("Could not find expected template child node " + nodePath);
                continue;
            }

            foreach (var (path, resourcePath) in nodeDict) {
                var separator = path.IndexOf(':');
                if (separator == -1) continue;

                var cls = path.Substring(0, separator);
                var valuePath = path.Substring(separator + 1);
                var comp = gameobj.GetComponent(cls);
                if (comp == null) continue;

                var resource = Importer.FindOrImportResource<Resource>(resourcePath, config);
                if (resource == null) {
                    GD.PrintErr("Stored template resource not found: " + resourcePath);
                    continue;
                }
                comp.SetFieldByPath(valuePath, resource);
            }
        }
    }
}