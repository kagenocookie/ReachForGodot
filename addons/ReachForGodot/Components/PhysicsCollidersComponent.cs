namespace RGE;

using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, Tool, REComponentClass("via.physics.Colliders")]
public partial class PhysicsCollidersComponent : REComponent, IVisualREComponent
{
    private StaticBody3D? meshNode;
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
        if (meshNode != null && !IsInstanceValid(meshNode)) {
            meshNode = null;
        }
        meshNode ??= GameObject.FindChildWhere<StaticBody3D>(child => child is StaticBody3D && child.Name == CollidersNodeName);
        if (!IsInstanceValid(meshNode)) {
            meshNode = null;
        }
        return meshNode;
    }

    public override void OnDestroy()
    {
        meshNode ??= GetOrFindContainerNode();
        if (meshNode != null) {
            if (!meshNode.IsQueuedForDeletion()) {
                meshNode.GetParent().CallDeferred(Node.MethodName.RemoveChild, meshNode);
                meshNode.QueueFree();
            }
            meshNode = null;
        }
    }

    public override async Task Setup(REGameObject gameObject, RszInstance rsz, RszImportType importType)
    {
        GameObject = gameObject;
        meshNode ??= GetOrFindContainerNode();

        if (meshNode == null) {
            meshNode = new StaticBody3D() { Name = CollidersNodeName };
            await gameObject.AddChildAsync(meshNode, gameObject.Owner ?? gameObject);
            await ReinstantiateCollisions(meshNode);
        } else {
            await ReinstantiateCollisions(meshNode);
        }
    }

    public override void PreExport()
    {
        // var resource = Importer.FindImportedResourceAsset(meshNode?.SceneFilePath) as MeshResource;
        // var meshScenePath = resource?.Asset?.AssetFilename;

        // SetField("Mesh", meshScenePath ?? string.Empty);
    }

    public Task ReinstantiateCollisions(Node3D node)
    {
        meshNode ??= GetOrFindContainerNode();
        var colliders = GetField(CollidersList).AsGodotArray<REObject>();
        node.ClearChildren();
        int n = 1;
        foreach (var coll in colliders) {
            var collider = new CollisionShape3D() { Name = "Collider_" + n++ + "_" + coll.Classname };
            node.AddChild(collider);
            var shape = coll.GetField(ColliderShapeField.Get(coll)).As<REObject>();
            if (shape == null) {

            } else {
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
            collider.Owner = GameObject.Owner;
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