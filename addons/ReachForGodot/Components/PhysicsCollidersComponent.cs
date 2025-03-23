namespace ReaGE;

using System.Globalization;
using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, Tool, REComponentClass("via.physics.Colliders")]
public partial class PhysicsCollidersComponent : REComponent, IVisualREComponent
{
    private StaticBody3D? colliderRoot;
    private static readonly StringName CollidersNodeName = "__Colliders";
    private static readonly REFieldAccessor CollidersList = new REFieldAccessor("Colliders").WithConditions(
        (list) => list.FirstOrDefault(f => f.RszField.array && f.RszField.type is not (RszFieldType.String or RszFieldType.Resource))
    );

    [REObjectFieldTarget("via.physics.Collider")]
    private static readonly REFieldAccessor ColliderShapeField = new REFieldAccessor("Shape", RszFieldType.Object).WithConditions(
        list => list.FirstOrDefault(f => f.RszField.original_type == "via.physics.Shape"),
        list => list.FirstOrDefault(f => f.RszField.type == RszFieldType.Object),
        list => list[2].RszField.type == RszFieldType.S32 ? list[2] : null,
        ((REFieldCondition)"v2").func);

    [REObjectFieldTarget("via.physics.Collider")]
    private static readonly REFieldAccessor CollisionFilterField = new REFieldAccessor("CollisionFilter", typeof(CollisionFilterResource)).WithConditions(
        list => list.FirstOrDefault(f => f.RszField.type is RszFieldType.Resource or RszFieldType.String));

    [REObjectFieldTarget("via.physics.SphereShape")]
    private static readonly REFieldAccessor SphereShape = new REFieldAccessor("Sphere", RszFieldType.Sphere).WithConditions(
        list => list.FirstOrDefault(f => f.RszField.type == RszFieldType.Sphere || f.RszField.type == RszFieldType.Vec4));

    [REObjectFieldTarget("via.physics.BoxShape")]
    private static readonly REFieldAccessor BoxShape = new REFieldAccessor("Box", RszFieldType.OBB).WithConditions(
        list => list.LastOrDefault(f => f.RszField.type == RszFieldType.OBB),
        list => list.LastOrDefault(f => !f.RszField.array && f.RszField.size == 80));

    [REObjectFieldTarget("via.physics.CapsuleShape")]
    private static readonly REFieldAccessor CapsuleShape = new REFieldAccessor("Capsule", RszFieldType.Capsule).WithConditions(
        list => list.FirstOrDefault(f => f.RszField.type == RszFieldType.Capsule),
        list => list.LastOrDefault(f => f.RszField.type == RszFieldType.Data && f.RszField.size == 32));

    [REObjectFieldTarget("via.physics.AabbShape")]
    private static readonly REFieldAccessor AabbShape = new REFieldAccessor("Aabb", RszFieldType.AABB).WithConditions(
        list => list.LastOrDefault(f => f.RszField.type == RszFieldType.AABB),
        list => list.Where(f => f.RszField.size == 32 && !f.RszField.array).LastOrDefault());

    [REObjectFieldTarget("via.physics.MeshShape")]
    private static readonly REFieldAccessor MeshShape = new REFieldAccessor("Mesh", typeof(REResource)).WithConditions(
        list => list.FirstOrDefault(f => f.RszField.type is RszFieldType.Resource or RszFieldType.String));

    public StaticBody3D? GetOrFindContainerNode()
    {
        if (colliderRoot != null && (!IsInstanceValid(colliderRoot) || colliderRoot.GetParent() != GameObject)) {
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

    public override async Task Setup(RszInstance rsz, RszImportType importType)
    {
        colliderRoot = GetOrFindContainerNode();

        if (colliderRoot == null) {
            colliderRoot = new StaticBody3D() { Name = CollidersNodeName };
            colliderRoot.LockNode(true);
            var owner = GameObject.FindRszOwnerNode();
            await GameObject.AddChildAsync(colliderRoot, owner);
            await UpdateColliders();
        } else {
            await UpdateColliders();
        }
    }

    public override void PreExport()
    {
        colliderRoot = GetOrFindContainerNode();
        if (colliderRoot == null) return;
        var colliders = GetField(CollidersList).AsGodotArray<REObject>();
        if (colliders == null) {
            SetField(CollidersList.Get(this), colliders = new Godot.Collections.Array<REObject>());
        }
        bool showWarning = false;
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
                        if (child.IsInsideTree() ? child.GlobalTransform.Basis.IsEqualApprox(Basis.Identity) : child.Name.ToString().Contains("AabbShape")) {
                            GetOrAddShape(Game, "via.physics.AabbShape", colliders, index, out shape, ref showWarning);
                            RequestSetCollisionShape3D.UpdateSerializedShape(shape, AabbShape, child.Shape, child, RcolFile.ShapeType.Aabb);
                        } else {
                            GetOrAddShape(Game, "via.physics.BoxShape", colliders, index, out shape, ref showWarning);
                            RequestSetCollisionShape3D.UpdateSerializedShape(shape, BoxShape, child.Shape, child, RcolFile.ShapeType.Box);
                        }
                        break;
                    case SphereShape3D sphere:
                        GetOrAddShape(Game, "via.physics.SphereShape", colliders, index, out shape, ref showWarning);
                        RequestSetCollisionShape3D.UpdateSerializedShape(shape, SphereShape, child.Shape, child, RcolFile.ShapeType.Sphere);
                        break;
                    case CapsuleShape3D capsule:
                        GetOrAddShape(Game, "via.physics.CapsuleShape", colliders, index, out shape, ref showWarning);
                        RequestSetCollisionShape3D.UpdateSerializedShape(shape, CapsuleShape, child.Shape, child, RcolFile.ShapeType.Capsule);
                        break;
                    case null:
                        break;
                    default:
                        GD.PrintErr("Unsupported collider type " + child.Shape.GetType() + " at " + Path);
                        break;
                }
            }
        }

        if (showWarning) {
            GD.Print("New colliders were created, some additional fields might be unset - please verify: " + Path);
        }

        static void GetOrAddShape(SupportedGame game, string classname, Godot.Collections.Array<REObject> colliders, int index, out REObject shape, ref bool warning)
        {
            var collider = index < colliders.Count ? colliders[index] : null;
            shape = collider == null ? new REObject() : collider.GetField(ColliderShapeField).As<REObject>();
            if (shape == null || shape.Classname != classname) {
                shape = new REObject(game, classname);
                shape.ResetProperties();
            }
            if (collider == null) {
                collider = new REObject(game, "via.physics.Collider");
                var firstCollider = colliders.FirstOrDefault();
                if (firstCollider == null) {
                    collider.ResetProperties();
                    warning = true;
                } else {
                    collider.ShallowCopyFrom(firstCollider);
                }
                colliders.Add(collider);
            }

            collider.SetField(ColliderShapeField, shape);
        }
    }

    private Task UpdateColliders()
    {
        Debug.Assert(colliderRoot != null);
        var colliders = GetField(CollidersList).AsGodotArray<REObject>();
        int n = 1;
        foreach (var coll in colliders) {
            var shape = coll.GetField(ColliderShapeField.Get(coll)).As<REObject>();
            var basename = "Collider_" + n++ + "_";
            var collider = colliderRoot.FindChildWhere<CollisionShape3D>(c => c.Name.ToString().StartsWith(basename));
            if (collider == null) {
                collider = new CollisionShape3D() { Name = basename + shape?.ClassBaseName };
                colliderRoot.AddChild(collider);
            }

            collider.Owner = colliderRoot.Owner;
            if (shape == null) {
                GD.Print("Missing collider shape " + n + " at " + Path);
                continue;
            }
            // TODO: DD2 has a BoundingAabb for all shapes in v0 - do we need to modify that too? RE2RT has an s32 instead.

            switch (shape.Classname) {
                case "via.physics.MeshShape":
                    // var mcol = shape.GetField(MeshShape).As<REResource>();
                    // collider.Shape = new ConvexPolygonShape3D();
                    break;
                case "via.physics.SphereShape":
                    RequestSetCollisionShape3D.ApplyShape(collider, RcolFile.ShapeType.Sphere, shape.GetField(SphereShape));
                    break;
                case "via.physics.BoxShape":
                    RequestSetCollisionShape3D.ApplyShape(collider, RcolFile.ShapeType.Box, shape.GetField(BoxShape));
                    break;
                case "via.physics.CapsuleShape":
                    RequestSetCollisionShape3D.ApplyShape(collider, RcolFile.ShapeType.Capsule, shape.GetField(CapsuleShape));
                    break;
                case "via.physics.AabbShape":
                    RequestSetCollisionShape3D.ApplyShape(collider, RcolFile.ShapeType.Aabb, shape.GetField(AabbShape));
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
        return GameObject.Transform * meshnode.GetNode3DAABB(false);
    }
}