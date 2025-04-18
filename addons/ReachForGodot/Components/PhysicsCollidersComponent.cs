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
    private static readonly REFieldAccessor CollidersList = new REFieldAccessor("Colliders")
        .Type(RszFieldType.Object)
        .OriginalType("via.physics.Collider")
        .Conditions((list) => list.FirstOrDefault(f => f.RszField.array && f.RszField.type is not (RszFieldType.String or RszFieldType.Resource))
    );

    [REObjectFieldTarget("via.physics.Collider")]
    private static readonly REFieldAccessor ColliderShapeField = new REFieldAccessor("Shape")
        .OriginalType("via.physics.Shape")
        .Conditions(
            list => list.FirstOrDefault(f => f.RszField.original_type == "via.physics.Shape"),
            list => list.FirstOrDefault(f => f.RszField.type == RszFieldType.Object),
            list => list[2].RszField.type == RszFieldType.S32 ? list[2] : null,
            ((REFieldCondition)"v2").func);

    [REObjectFieldTarget("via.physics.Collider")]
    private static readonly REFieldAccessor CollisionFilterField = new REFieldAccessor("CollisionFilter")
        .Resource<CollisionFilterResource>()
        .Conditions(list => list.FirstOrDefault(f => f.RszField.type is RszFieldType.Resource or RszFieldType.String));

    [REObjectFieldTarget("via.physics.SphereShape")]
    private static readonly REFieldAccessor SphereShape = new REFieldAccessor("Sphere").Type(RszFieldType.Sphere).Conditions(
        list => list.FirstOrDefault(f => f.RszField.type == RszFieldType.Sphere || f.RszField.type == RszFieldType.Vec4));

    [REObjectFieldTarget("via.physics.ContinuousSphereShape")]
    private static readonly REFieldAccessor ContinuousSphereShape = new REFieldAccessor("Sphere").Type(RszFieldType.Sphere).Conditions(
        list => list.FirstOrDefault(f => f.RszField.type == RszFieldType.Sphere || f.RszField.type == RszFieldType.Vec4));

    [REObjectFieldTarget("via.physics.BoxShape")]
    private static readonly REFieldAccessor BoxShape = new REFieldAccessor("Box").Type(RszFieldType.OBB).Conditions(
        list => list.LastOrDefault(f => f.RszField.type == RszFieldType.OBB),
        list => list.LastOrDefault(f => !f.RszField.array && f.RszField.size == 80));

    [REObjectFieldTarget("via.physics.CapsuleShape")]
    private static readonly REFieldAccessor CapsuleShape = new REFieldAccessor("Capsule").Type(RszFieldType.Capsule).Conditions(
        list => list.FirstOrDefault(f => f.RszField.type == RszFieldType.Capsule),
        list => list.LastOrDefault(f => f.RszField.type == RszFieldType.Data && f.RszField.size == 48));

    [REObjectFieldTarget("via.physics.ContinuousCapsuleShape")]
    private static readonly REFieldAccessor ContinuousCapsuleShape = new REFieldAccessor("Capsule").Type(RszFieldType.Capsule).Conditions(
        list => list.FirstOrDefault(f => f.RszField.type == RszFieldType.Capsule),
        list => list.LastOrDefault(f => f.RszField.type == RszFieldType.Data && f.RszField.size == 48));

    [REObjectFieldTarget("via.physics.AabbShape")]
    private static readonly REFieldAccessor AabbShape = new REFieldAccessor("Aabb").Type(RszFieldType.AABB).Conditions(
        list => list.LastOrDefault(f => f.RszField.type == RszFieldType.AABB),
        list => list.Where(f => f.RszField.size == 32 && !f.RszField.array).LastOrDefault());

    [REObjectFieldTarget("via.physics.MeshShape")]
    private static readonly REFieldAccessor MeshShape = new REFieldAccessor("Mesh")
        .Resource<MeshResource>()
        .Conditions(list => list.FirstOrDefault(f => f.RszField.type is RszFieldType.Resource or RszFieldType.String));

    [REObjectFieldTarget("via.physics.StaticCompoundShape")]
    private static readonly REFieldAccessor CompoundShapesList = new REFieldAccessor("Shapes")
        .OriginalType("via.physics.StaticCompoundShape.Instance")
        .Conditions(list => list.FirstOrDefault(f => f.RszField.array));

    [REObjectFieldTarget("via.physics.StaticCompoundShape.Instance")]
    private static readonly REFieldAccessor CompoundShapeInstanceShape = new REFieldAccessor("Shapes")
        .OriginalType("via.physics.Shape")
        .Conditions(list => list.FirstOrDefault(f => f.RszField.size == 4));

    [ExportToolButton("Generate collider nodes")]
    private Callable GenerateColliderNodesBtn => Callable.From(() => { _ = GenerateColliderNodes(); });

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

    public override async Task Setup(RszImportType importType)
    {
        await GenerateColliderNodes();
    }

    private async Task GenerateColliderNodes()
    {
        colliderRoot = GetOrFindContainerNode();
        if (colliderRoot == null) {
            colliderRoot = new StaticBody3D() { Name = CollidersNodeName };
            colliderRoot.LockNode(true);
            var owner = GameObject.FindRszOwnerNode();
            await GameObject.AddChildAsync(colliderRoot, owner);
        }
        await ImportColliders();
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

                // TODO handle via.physics.StaticCompoundShape
                switch (child.Shape) {
                    case BoxShape3D box:
                        if (child.IsInsideTree() ? child.GlobalTransform.Basis.IsEqualApprox(Basis.Identity) : child.Name.ToString().Contains("AabbShape")) {
                            GetOrAddShape(Game, "via.physics.AabbShape", null, colliders, index, out shape, ref showWarning);
                            RequestSetCollisionShape3D.UpdateSerializedShape(shape, AabbShape, child.Shape, child, RszTool.Rcol.ShapeType.Aabb);
                        } else {
                            GetOrAddShape(Game, "via.physics.BoxShape", null, colliders, index, out shape, ref showWarning);
                            RequestSetCollisionShape3D.UpdateSerializedShape(shape, BoxShape, child.Shape, child, RszTool.Rcol.ShapeType.Box);
                        }
                        break;
                    case SphereShape3D sphere:
                        GetOrAddShape(Game, "via.physics.SphereShape", "via.physics.ContinuousSphereShape", colliders, index, out shape, ref showWarning);
                        RequestSetCollisionShape3D.UpdateSerializedShape(shape, SphereShape, child.Shape, child, RszTool.Rcol.ShapeType.Sphere);
                        break;
                    case CapsuleShape3D capsule:
                        GetOrAddShape(Game, "via.physics.CapsuleShape", "via.physics.ContinuousCapsuleShape", colliders, index, out shape, ref showWarning);
                        RequestSetCollisionShape3D.UpdateSerializedShape(shape, CapsuleShape, child.Shape, child, RszTool.Rcol.ShapeType.Capsule);
                        break;
                    case null:
                        break;
                    default:
                        GD.PrintErr("Unsupported export for collider type " + child.Shape.GetType() + " at " + Path);
                        break;
                }
            }
        }

        if (showWarning) {
            GD.Print("New colliders were created, some additional fields might be unset - please verify: " + Path);
        }

        static void GetOrAddShape(SupportedGame game, string classname, string? classname2, Godot.Collections.Array<REObject> colliders, int index, out REObject shape, ref bool warning)
        {
            var collider = index < colliders.Count ? colliders[index] : null;
            shape = collider == null ? new REObject() : collider.GetField(ColliderShapeField).As<REObject>();
            if (shape == null || shape.Classname != classname && (classname2 == null || shape.Classname != classname2)) {
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

    private Task ImportColliders()
    {
        Debug.Assert(colliderRoot != null);
        var colliders = GetField(CollidersList).AsGodotArray<REObject>();
        int n = 1;
        foreach (var coll in colliders) {
            var basename = "Collider_" + n++;
            var shape = coll.GetField(ColliderShapeField).As<REObject>();
            CollisionShape3D? collider = FindOrCreateCollider(colliderRoot, basename, shape);
            if (shape == null) {
                GD.Print("Missing collider shape " + n + " at " + Path);
                continue;
            }

            ImportColliderShape(shape, collider);
        }
        return Task.CompletedTask;
    }

    private CollisionShape3D FindOrCreateCollider(StaticBody3D parent, string basename, REObject? shape)
    {
        var collider = parent.FindChildWhere<CollisionShape3D>(c => c.Name.ToString().StartsWith(basename));
        if (collider == null) {
            collider = new CollisionShape3D() { Name = basename + "_" + shape?.ClassBaseName };
            parent.AddChild(collider);
        }

        collider.Owner = parent.Owner;
        if (shape != null) {
            ImportColliderShape(shape, collider);
        }
        return collider;
    }

    private void ImportColliderShape(REObject shape, CollisionShape3D collider)
    {
        switch (shape.Classname) {
            case "via.physics.MeshShape":
                // var mcol = shape.GetField(MeshShape).As<REResource>();
                // collider.Shape = new ConvexPolygonShape3D();
                break;
            case "via.physics.SphereShape":
            case "via.physics.ContinuousSphereShape":
                RequestSetCollisionShape3D.ApplyShape(collider, RszTool.Rcol.ShapeType.Sphere, shape.GetField(SphereShape));
                break;
            case "via.physics.BoxShape":
                RequestSetCollisionShape3D.ApplyShape(collider, RszTool.Rcol.ShapeType.Box, shape.GetField(BoxShape));
                break;
            case "via.physics.CapsuleShape":
            case "via.physics.ContinuousCapsuleShape":
                RequestSetCollisionShape3D.ApplyShape(collider, RszTool.Rcol.ShapeType.Capsule, shape.GetField(CapsuleShape));
                break;
            case "via.physics.AabbShape":
                RequestSetCollisionShape3D.ApplyShape(collider, RszTool.Rcol.ShapeType.Aabb, shape.GetField(AabbShape));
                break;
            case "via.physics.StaticCompoundShape":
                var shapes = shape.GetField(CompoundShapesList).AsGodotArray<REObject>();
                var subcount = 0;
                var basename = collider.Name;
                foreach (var subshape in shapes.Select(sh => sh.GetField(CompoundShapeInstanceShape).As<REObject>())) {
                    if (subcount > 0) {
                        var newcoll = FindOrCreateCollider(collider.GetParent<StaticBody3D>(), basename + "_" + subcount++, subshape);
                    } else {
                        collider.Name = basename + "_" + subcount++;
                        ImportColliderShape(subshape, collider);
                    }
                }
                break;
            default:
                GD.Print("Unhandled collider shape " + shape.Classname);
                break;
        }
    }

    public Aabb GetBounds()
    {
        var meshnode = GetOrFindContainerNode();
        if (meshnode == null) return new Aabb();
        return GameObject.Transform * meshnode.GetNode3DAABB(false);
    }
}