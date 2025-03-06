namespace RGE;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, Tool, REComponentClass("via.physics.Colliders")]
public partial class PhysicsCollidersComponent : REComponent, IVisualREComponent
{
    private StaticBody3D? colliderRoot;
    private static readonly StringName CollidersNodeName = "__Colliders";
    private static readonly REObjectFieldAccessor CollidersList = new REObjectFieldAccessor(
        list => list.FirstOrDefault(f => f.RszField.array && f.RszField.type is not (RszFieldType.String or RszFieldType.Resource)));

    [REObjectFieldTarget("via.physics.Collider")]
    private static readonly REObjectFieldAccessor ColliderShapeField = new REObjectFieldAccessor(
        list => list.FirstOrDefault(f => f.RszField.type == RszFieldType.Object));

    [REObjectFieldTarget("via.physics.SphereShape")]
    private static readonly REObjectFieldAccessor SphereShape = new REObjectFieldAccessor(
        list => list.FirstOrDefault(f => f.RszField.type == RszFieldType.Sphere || f.RszField.type == RszFieldType.Vec4));

    [REObjectFieldTarget("via.physics.BoxShape")]
    private static readonly REObjectFieldAccessor BoxShape = new REObjectFieldAccessor(
        list => list.FirstOrDefault(f => f.RszField.type == RszFieldType.OBB));

    [REObjectFieldTarget("via.physics.CapsuleShape")]
    private static readonly REObjectFieldAccessor CapsuleShape = new REObjectFieldAccessor(
        list => list.FirstOrDefault(f => f.RszField.type == RszFieldType.Capsule));

    [REObjectFieldTarget("via.physics.MeshShape")]
    private static readonly REObjectFieldAccessor MeshShape = new REObjectFieldAccessor(
        "Mesh",
        list => list.FirstOrDefault(f => f.RszField.type is RszFieldType.Resource or RszFieldType.String),
        mod => mod.MarkAsResource(nameof(REResource)));

    public StaticBody3D? GetOrFindContainerNode()
    {
        if (colliderRoot != null && !IsInstanceValid(colliderRoot)) {
            colliderRoot = null;
        }
        colliderRoot ??= GameObject.FindChildWhere<StaticBody3D>(child => child is StaticBody3D && child.Name == CollidersNodeName);
        if (!IsInstanceValid(colliderRoot)) {
            colliderRoot = null;
        }
        return colliderRoot;
    }

    public override void OnDestroy()
    {
        colliderRoot ??= GetOrFindContainerNode();
        if (colliderRoot != null) {
            if (!colliderRoot.IsQueuedForDeletion()) {
                colliderRoot.GetParent().CallDeferred(Node.MethodName.RemoveChild, colliderRoot);
                colliderRoot.QueueFree();
            }
            colliderRoot = null;
        }
    }

    public override async Task Setup(REGameObject gameObject, RszInstance rsz, RszImportType importType)
    {
        GameObject = gameObject;
        colliderRoot ??= GetOrFindContainerNode();

        if (colliderRoot == null) {
            colliderRoot = new StaticBody3D() { Name = CollidersNodeName };
            await gameObject.AddChildAsync(colliderRoot, gameObject.Owner ?? gameObject);
            await ReinstantiateCollisions(colliderRoot);
        } else {
            await ReinstantiateCollisions(colliderRoot);
        }
    }

    public override void PreExport()
    {
        colliderRoot ??= GetOrFindContainerNode();
        if (colliderRoot == null) return;
        var colliders = GetField(CollidersList).AsGodotArray<REObject>();
        if (colliders == null) {
            SetField(CollidersList.Get(this), colliders = new Godot.Collections.Array<REObject>());
        }
        foreach (var child in colliderRoot.FindChildrenByType<CollisionShape3D>()) {
            var name = child.Name.ToString();
            var sub1 = name.IndexOf('_');
            var sub2 = sub1 == -1 ? -1 : name.IndexOf('_', sub1 + 1);
            if (sub2 == -1) continue;
            if (int.TryParse(name.AsSpan()[(sub1 + 1)..sub2], CultureInfo.InvariantCulture, out var id)) {
                var index = id - 1;
                REObject shape;
                switch (child.Shape) {
                    case BoxShape3D box:
                        GetOrAddShape(Game, "via.physics.BoxShape", colliders, index, out shape);
                        var obb = shape.GetField(MeshShape).As<OrientedBoundingBox>();
                        obb.extent = box.Size;
                        obb.coord = new Projection(child.Transform);
                        break;
                    case SphereShape3D sphere:
                        GetOrAddShape(Game, "via.physics.SphereShape", colliders, index, out shape);
                        var pos = child.Position;
                        shape.SetField(SphereShape, new Vector4(pos.X, pos.Y, pos.Z, sphere.Radius));
                        break;
                    case CapsuleShape3D capsule:
                        GetOrAddShape(Game, "via.physics.CapsuleShape", colliders, index, out shape);
                        var cap = shape.GetField(MeshShape).As<Capsule>();
                        cap.r = capsule.Radius;
                        var cappos = child.Position;
                        var up = child.Transform.Basis.Y.Normalized();
                        cap.p0 = cappos - up * 0.5f * capsule.Height;
                        cap.p1 = cappos + up * 0.5f * capsule.Height;
                        break;
                    case null:
                        break;
                    default:
                        GD.PrintErr("Unsupported collider type " + child.Shape.GetType() + " at " + Path);
                        break;
                }
            }
        }

        static void GetOrAddShape(SupportedGame game, string classname, Godot.Collections.Array<REObject> colliders, int index, out REObject shape)
        {
            var collider = colliders.Count > index ? colliders[index] : null!;
            shape = collider == null ? new REObject() : collider.GetField(ColliderShapeField).As<REObject>();
            if (shape == null || shape.Classname != classname) {
                shape = new REObject(game, classname);
                if (colliders.Count <= index) {
                    colliders.Add(shape);
                } else {
                    colliders[index] = shape;
                }
                shape.ResetProperties();
            }
        }
    }

    public Task ReinstantiateCollisions(Node3D node)
    {
        colliderRoot ??= GetOrFindContainerNode();
        var colliders = GetField(CollidersList).AsGodotArray<REObject>();
        node.ClearChildren();
        int n = 1;
        foreach (var coll in colliders) {
            var collider = new CollisionShape3D() { Name = "Collider_" + n++ + "_" + coll.Classname };
            node.AddChild(collider);
            collider.Owner = GameObject.Owner ?? GameObject;
            var shape = coll.GetField(ColliderShapeField.Get(coll)).As<REObject>();
            if (shape == null) {
                GD.Print("Missing collider shape " + n + " at " + Path);
                continue;
            }

            switch (shape.Classname) {
                case "via.physics.MeshShape":
                    // var mcol = shape.GetField(MeshShape).As<REResource>();
                    // collider.Shape = new ConvexPolygonShape3D();
                    break;
                case "via.physics.SphereShape":
                    var sphere = shape.GetField(SphereShape).AsVector4();
                    collider.Shape = new SphereShape3D() { Radius = sphere.W };
                    collider.Position = sphere.ToVector3();
                    break;
                case "via.physics.BoxShape":
                    var obb = shape.GetField(BoxShape).As<OrientedBoundingBox>();
                    obb.extent.X = Mathf.Max(0.001f, Mathf.Abs(obb.extent.X));
                    obb.extent.Y = Mathf.Max(0.001f, Mathf.Abs(obb.extent.Y));
                    obb.extent.Z = Mathf.Max(0.001f, Mathf.Abs(obb.extent.Z));
                    collider.Shape = new BoxShape3D() { Size = obb.extent };
                    collider.Transform = (Transform3D)obb.coord;
                    break;
                case "via.physics.CapsuleShape":
                    var capsule = shape.GetField(BoxShape).As<Capsule>();
                    collider.Shape = new CapsuleShape3D() { Height = capsule.p0.DistanceTo(capsule.p1), Radius = capsule.r };
                    collider.Position = (capsule.p0 + capsule.p1) / 2;
                    break;
                default:
                    GD.Print("Unhandled collider shape " + shape.Classname);
                    break;
            }
        }
        return Task.CompletedTask;
    }

    public Aabb GetBounds()
    {
        var meshnode = GetOrFindContainerNode();
        if (meshnode == null) return new Aabb();
        return meshnode.GetNode3DAABB(false);
    }
}