namespace ReaGE;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public static class GodotObjectExtensions
{
    public static void SetActive(this CanvasItem target, bool active)
    {
        if (active) {
            target.Show();
        } else {
            target.Hide();
        }
    }

    /// <summary>
    /// Find an immediate child of a node by type.
    /// </summary>
    public static T? FindChildByType<T>(this Node node) where T : class
    {
        var count = node.GetChildCount();
        for (int i = 0; i < count; ++i) {
            var child = node.GetChild(i);
            if (child is T target)
                return target;
        }

        return null;
    }

    /// <summary>
    /// Find a node of a specific type in any parent node.
    /// </summary>
    public static bool TryFindChildByType<T>(this Node node, [MaybeNullWhen(false)] out T value) where T : class
    {
        value = node.FindChildByType<T>();
        return value != null;
    }

    /// <summary>
    /// Find an immediate child of a node by type. Throw an exception if it is not found.
    /// </summary>
    public static T RequireChildByType<T>(this Node node, string? error = null) where T : class
    {
        return FindChildByType<T>(node)
            ?? throw new ArgumentException(error ?? $"Node {node.GetPath()} does not have a required direct child of type {typeof(T)}");
    }

    /// <summary>
    /// Find a child of a node by type.
    /// </summary>
    public static T? FindChildByTypeRecursive<T>(this Node node) where T : class
    {
        var count = node.GetChildCount();
        for (int i = 0; i < count; ++i) {
            var child = node.GetChild(i);
            if (child is T target)
                return target;

            var recChild = child.FindChildByTypeRecursive<T>();
            if (recChild != null) {
                return recChild;
            }
        }

        return null;
    }

    /// <summary>
    /// Find an immediate child of a node by type. Throw an exception if it is not found.
    /// </summary>
    public static T RequireChildByTypeRecursive<T>(this Node node, string? error = null) where T : class
    {
        return FindChildByTypeRecursive<T>(node)
            ?? throw new ArgumentException(error ?? $"Node {node.GetPath()} does not have a required recursive child of type {typeof(T)}");
    }

    /// <summary>
    /// Find an immediate child of a node by type.
    /// </summary>
    public static T? FindChildWhere<T>(this Node node, Func<T, bool> filter) where T : class
    {
        var count = node.GetChildCount();
        for (int i = 0; i < count; ++i) {
            var child = node.GetChild(i);
            if (child is T target && filter(target))
                return target;
        }

        return null;
    }

    /// <summary>
    /// Find an immediate child of a node by type.
    /// </summary>
    public static T? FindChildWhereRecursive<T>(this Node node, Func<T, bool> filter) where T : class
    {
        var count = node.GetChildCount();
        for (int i = 0; i < count; ++i) {
            var child = node.GetChild(i);
            if (child is T target && filter(target))
                return target;

            var recChild = child.FindChildWhereRecursive<T>(filter);
            if (recChild != null) {
                return recChild;
            }
        }

        return null;
    }

    /// <summary>
    /// Find a node of a specific type in any parent node.
    /// </summary>
    public static T? FindNodeInParents<T>(this Node node) where T : class
    {
        var parent = node.GetParent();
        while (parent != null) {
            if (parent is T cast) {
                return cast;
            }

            parent = parent.GetParent();
        }

        return null;
    }

    /// <summary>
    /// Find a node of a specific type in any parent node.
    /// </summary>
    public static IEnumerable<T> FindParentsByType<T>(this Node node) where T : class
    {
        var parent = node.GetParent();
        while (parent != null) {
            if (parent is T cast) {
                yield return cast;
            }

            parent = parent.GetParent();
        }
    }

    /// <summary>
    /// Find a node of a specific type in any parent node.
    /// </summary>
    public static bool TryFindNodeInParents<T>(this Node node, [MaybeNullWhen(false)] out T? result) where T : class
    {
        var parent = node.GetParent();
        while (parent != null) {
            if (parent is T cast) {
                result = cast;
                return true;
            }

            parent = parent.GetParent();
        }

        result = null;
        return false;
    }

    /// <summary>
    /// Find a node of a specific type in any parent node.
    /// </summary>
    public static T? FindNodeInParents<T>(this Node node, Func<T, bool> condition) where T : class
    {
        var parent = node.GetParent();
        while (parent != null) {
            if (parent is T cast && condition(cast)) {
                return cast;
            }

            parent = parent.GetParent();
        }

        return null;
    }

    public static void FreeAllChildren(this Node node)
    {
        foreach (var child in node.GetChildren()) {
            child.QueueFree();
        }
    }

    public static void FreeAllChildrenImmediately(this Node node)
    {
        foreach (var child in node.GetChildren()) {
            child.Free();
        }
    }

    public static void ClearChildren(this Node node)
    {
        var i = node.GetChildCount();
        while (--i >= 0) {
            var child = node.GetChild(i);
            node.RemoveChild(child);
            child.Free();
        }
    }

    public static async Task<T> AddChildAsync<T>(this Node parent, T child, Node? owner) where T : Node
    {
        if (child.GetParent() != null) {
            child.CallDeferred(Node.MethodName.Reparent, parent);
        } else {
            parent.CallDeferred(Node.MethodName.AddChild, child);
        }
        owner ??= parent;
        child.SetDeferred(Node.PropertyName.Owner, owner);
        while (child.Owner != owner && child.GetParent() != parent) {
            await Task.Delay(1);
        }
        return child;
    }

    public static T AddUniqueNamedChild<T>(this Node parent, T child, string separator = "___") where T : Node
    {
        var basename = child.Name.ToString();
        var currentSeparatorPos = basename.IndexOf(separator);
        if (currentSeparatorPos != -1 && int.TryParse(basename.AsSpan()[(currentSeparatorPos + separator.Length)..], out _)) {
            basename = basename.Substr(0, currentSeparatorPos);
        }
        if (parent.FindChild(basename) != null) {
            var index = 1;
            string nextname;
            do {
                nextname = basename + separator + (index++);
            } while (parent.FindChild(nextname) != null);
            child.Name = nextname;
        }
        parent.AddChild(child);
        child.Owner = parent.Owner ?? parent;
        return child;
    }

    public static void SetRecursiveOwner(this Node node, Node owner, Node? sourceOwner = null)
    {
        node.Owner = owner;
        foreach (var child in node.GetChildren()) {
            if (sourceOwner != null ? child.Owner == sourceOwner : string.IsNullOrEmpty(child.SceneFilePath)) {
                SetRecursiveOwner(child, owner);
            } else {
                child.Owner = owner;
            }
        }
    }

    public static void EmplaceChild(this Node parent, Node previousNode, Node newNode)
    {
        var index = previousNode.GetIndex();
        var owner = previousNode.Owner;
        newNode.Name = previousNode.Name + "__emplace_temp";
        if (newNode.GetParent() != null) {
            newNode.Reparent(parent);
        } else {
            parent.AddChild(newNode);
        }
        parent.MoveChild(newNode, index);
        parent.RemoveChild(previousNode);
        newNode.Name = previousNode.Name;
        newNode.Owner = owner;
    }

    /// <summary>
    /// Find a node of a specific type in any parent node.
    /// </summary>
    public static bool TryGetNodeInParents<T>(this Node node, [MaybeNullWhen(false)] out T value) where T : class
    {
        value = node.FindNodeInParents<T>();
        return value != null;
    }

    /// <summary>
    /// Find a node of a specific type in any parent node. Throw an exception if not found.
    /// </summary>
    public static T RequireNodeInParents<T>(this Node node) where T : class
    {
        return FindNodeInParents<T>(node)
            ?? throw new ArgumentException($"Node {node.GetPath()} does not have a required parent of type {typeof(T)}");
    }

    public static IEnumerable<T> FindChildrenByType<T>(this Node node) where T : class
    {
        var count = node.GetChildCount();
        for (int i = 0; i < count; ++i) {
            var child = node.GetChild(i);
            if (child is T target)
                yield return target;
        }
    }

    public static IEnumerable<T> FindChildrenByTypeRecursive<T>(this Node node) where T : class
    {
        var count = node.GetChildCount();
        for (int i = 0; i < count; ++i) {
            var child = node.GetChild(i);
            if (child is T target)
                yield return target;

            foreach (var subnode in child.FindChildrenByTypeRecursive<T>()) {
                yield return subnode;
            }
        }
    }
    public static IEnumerable<T> FindChildrenWhere<T>(this Node node, Func<T, bool> condition) where T : class
    {
        var count = node.GetChildCount();
        for (int i = 0; i < count; ++i) {
            var child = node.GetChild(i);
            if (child is T target && condition(target))
                yield return target;
        }
    }

    public static IEnumerable<T> FindChildrenRecursive<T>(this Node node, Func<T, bool> condition) where T : class
    {
        var count = node.GetChildCount();
        for (int i = 0; i < count; ++i) {
            var child = node.GetChild(i);
            if (child is T target && condition(target))
                yield return target;

            foreach (var subnode in child.FindChildrenRecursive<T>(condition)) {
                yield return subnode;
            }
        }
    }

    /// <summary>
    /// Transforms a direction from world space to local space. The opposite of Transform.TransformDirection.
    /// </summary>
    public static Vector3 InverseTransformDirection(this Transform3D transform, Vector3 direction)
    {
        return transform.Basis.Inverse() * direction;
    }

    /// <summary>
    /// Transforms direction from local space to world space.
    /// </summary>
    public static Vector3 TransformDirection(this Node3D transform, Vector3 direction)
    {
        return transform.GlobalBasis * direction;
    }

    /// <summary>Get a transform rotated by the rotation between two direction vectors. Keep in mind that scale will be lost.</summary>
    public static Transform3D RotateByFromToDirection(this Transform3D transform, Vector3 fromDirection, Vector3 toDirection)
    {
        var extraRotation = new Quaternion(fromDirection, toDirection);
        var rotation = extraRotation * transform.Basis.GetRotationQuaternion();
        return new Transform3D(new Basis(rotation), transform.Origin);
    }

    /// <summary>Change a transform's rotation to have a local axis look at a chosen direction. Keep in mind that scale will be lost.</summary>
    public static Transform3D LookAtAlongAxis(this Transform3D transform, Vector3 axis, Vector3 direction)
    {
        var extraRotation = new Quaternion(transform.Inverse() * axis, direction);
        var rotation = extraRotation * transform.Basis.GetRotationQuaternion();
        return new Transform3D(new Basis(rotation), transform.Origin);
    }

    public static void SetPositionAndRotation(this Node3D transform, Vector3 position, Quaternion rotation)
    {
        transform.GlobalPosition = position;
        transform.GlobalRotation = rotation.GetEuler();
    }

    /// <summary>Get the forward vector from a transform (-Z)</summary>
    public static Vector3 Forward(this Transform3D transform) => -transform.Basis.Z.Normalized();
    /// <summary>Get the back vector from a transform (+Z)</summary>
    public static Vector3 Back(this Transform3D transform) => transform.Basis.Z.Normalized();
    /// <summary>Get the up vector from a transform (+Y)</summary>
    public static Vector3 Up(this Transform3D transform) => transform.Basis.Y.Normalized();
    /// <summary>Get the down vector from a transform (-Y)</summary>
    public static Vector3 Down(this Transform3D transform) => -transform.Basis.Y.Normalized();
    /// <summary>Get the right vector from a transform (+X)</summary>
    public static Vector3 Right(this Transform3D transform) => transform.Basis.X.Normalized();
    /// <summary>Get the left vector from a transform (-X)</summary>
    public static Vector3 Left(this Transform3D transform) => -transform.Basis.X.Normalized();

    /// <summary>Get the forward vector from a basis (-Z)</summary>
    public static Vector3 Forward(this Basis basis) => -basis.Z.Normalized();
    /// <summary>Get the back vector from a basis (+Z)</summary>
    public static Vector3 Back(this Basis basis) => basis.Z.Normalized();
    /// <summary>Get the up vector from a basis (+Y)</summary>
    public static Vector3 Up(this Basis basis) => basis.Y.Normalized();
    /// <summary>Get the down vector from a basis (-Y)</summary>
    public static Vector3 Down(this Basis basis) => -basis.Y.Normalized();
    /// <summary>Get the right vector from a basis (+X)</summary>
    public static Vector3 Right(this Basis basis) => basis.X.Normalized();
    /// <summary>Get the left vector from a basis (-X)</summary>
    public static Vector3 Left(this Basis basis) => -basis.X.Normalized();

    public static Godot.Collections.Dictionary Raycast(this CollisionObject3D target, Vector3 from, Vector3 to)
    {
        var spaceState = target.GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(from, to, target.CollisionLayer);
        return spaceState.IntersectRay(query);
    }

    public static float GetClosestOffsetOnPath(this Path3D path, Vector3 origin)
    {
        return path.Curve.GetClosestOffset(path.Transform.Inverse() * origin);
    }

    public static string ToStringForPropertyList(this Godot.Collections.Array<Godot.Collections.Dictionary> list)
    {
        return string.Join("\n", list.Select(dict => string.Join(", ", dict.Select(kv => kv.Key + "=" + kv.Value))));
    }

    public static Godot.Aabb GetNode3DAABB(this Node parent, bool excludeTopLevel)
    {
        var bounds = new Godot.Aabb();
        if (parent is VisualInstance3D vis) {
            bounds = vis.GetAabb();
        }

        for (var i = 0; i < parent!.GetChildCount(); ++i) {
            var child = parent.GetChild(i);
            var child_bounds = GetNode3DAABB(child, false);
            if (bounds.Size == Vector3.Zero && parent != null) {
                bounds = child_bounds;
            } else if (child_bounds.Size != Vector3.Zero) {
                bounds = bounds.Merge(child_bounds);
            }
        }

        if (!excludeTopLevel && parent is Node3D parent3d) {
            bounds = parent3d.Transform * bounds;
        }

        return bounds;
    }

    /// <summary>
    /// Get the actual global transform of a bone, relative to world instead of the skeleton.
    /// </summary>
    public static Transform3D GetRealBoneGlobalPose(this Skeleton3D skeleton, int boneIndex)
    {
        var pose = skeleton.GetBoneGlobalPose(boneIndex);
        return skeleton.GlobalTransform * pose;
    }

    public static string ResourceFilename(this Resource resource) => resource.ResourcePath.GetFile().GetBaseName();

    public static void DisconnectAllSignals(this Node node, StringName? signalName)
    {
        var signals = signalName == null ? node.GetSignalList() : node.GetSignalConnectionList(signalName);
        foreach (var sig in signals) {
            node.Disconnect(sig["signal"].AsStringName(), sig["callable"].AsCallable());
        }
    }

    public static Vector3 ToVector3(this Vector4 vec) => new Vector3(vec.X, vec.Y, vec.Z);
    public static Quaternion ToQuaternion(this Vector4 vec) => new Quaternion(vec.X, vec.Y, vec.Z, vec.W);
}
