namespace ReaGE;

using System;
using Godot;
using ReeLib;

[GlobalClass, Tool]
public partial class RequestSetCollisionShape3D : CollisionShape3D
{
    [Export] public ReeLib.Rcol.ShapeType RcolShapeType { get; set; }
    [Export] public string? Uuid { get; private set; }
    [Export] public string? OriginalName { get; set; }

    [Export] public int LayerIndex { get; set; }
    [Export] public uint SkipIdBits { get; set; }
    [Export] public uint IgnoreTagBits { get; set; }
    [Export] public int Attribute { get; set; }
    [Export] public string? PrimaryJointNameStr { get; set; }
    [Export] public string? SecondaryJointNameStr { get; set; }
    [Export] public bool IsExtraShape { get; set; }

    [Export] public REObject? Data { get; set; }
    [Export] public Godot.Collections.Dictionary<uint, REObject>? SetDatas { get; set; }

    public Guid Guid {
        get => Guid.TryParse(Uuid, out var guid) ? guid : Guid.Empty;
        set => Uuid = value.ToString();
    }

    public ReeLib.Rcol.RcolShape ToRsz(SupportedGame game)
    {
        var shape = new ReeLib.Rcol.RcolShape();
        shape.Info.Guid = Guid;
        shape.Info.Name = OriginalName ?? string.Empty;
        shape.Info.primaryJointNameStr = PrimaryJointNameStr ?? string.Empty;
        shape.Info.secondaryJointNameStr = SecondaryJointNameStr ?? string.Empty;
        shape.Info.LayerIndex = LayerIndex;
        shape.Info.SkipIdBits = SkipIdBits;
        shape.Info.IgnoreTagBits = IgnoreTagBits;
        shape.Info.Attribute = Attribute;
        shape.Info.shapeType = RcolShapeType;
        shape.shape = RequestSetCollisionShape3D.ConvertShapeToRsz(Shape, this, RcolShapeType, game);
        return shape;
    }

    public override string ToString() => Name;

    public static void ApplyShape(CollisionShape3D collider, ReeLib.Rcol.ShapeType shapeType, Variant shape)
    {
        switch (shapeType) {
            case ReeLib.Rcol.ShapeType.Sphere:
            case ReeLib.Rcol.ShapeType.ContinuousSphere:
                var sphere = shape.AsVector4();
                collider.Shape = new SphereShape3D() { Radius = sphere.W };
                collider.Position = sphere.ToVector3();
                break;
            case ReeLib.Rcol.ShapeType.Box:
                var obb = shape.As<OrientedBoundingBox>();
                // TODO: sometime the obb extents are all == 0. should we use the BoundingAabb for size instead?
                obb.extent.X = Mathf.Max(0.001f, Mathf.Abs(obb.extent.X));
                obb.extent.Y = Mathf.Max(0.001f, Mathf.Abs(obb.extent.Y));
                obb.extent.Z = Mathf.Max(0.001f, Mathf.Abs(obb.extent.Z));
                collider.Shape = new BoxShape3D() { Size = obb.extent * 2 };
                collider.Transform = (Transform3D)obb.coord;
                break;
            case ReeLib.Rcol.ShapeType.Capsule:
            case ReeLib.Rcol.ShapeType.ContinuousCapsule:
                var capsule = shape.As<Capsule>();
                // RE format capsule: p0 and p1 are placed at the cylinder center, whereas godot places it at the far end of the capsule, which is why we need to add the radius
                collider.Shape = new CapsuleShape3D() { Height = capsule.p0.DistanceTo(capsule.p1) + capsule.r * 2, Radius = capsule.r };
                collider.Position = (capsule.p0 + capsule.p1) / 2;
                collider.Rotation = (capsule.p1 - capsule.p0).DirectionToQuaternion(Vector3.Up).GetEuler();
                break;
            case ReeLib.Rcol.ShapeType.Aabb:
                var aabb = shape.As<Aabb>();
                FixNegativeAabb(ref aabb);
                if (aabb.Size.X < 0) { aabb.Position = new Vector3(aabb.Position.X + aabb.Size.X, aabb.Position.Y, aabb.Position.Z); }
                collider.Shape = new BoxShape3D() { Size = aabb.Size };
                collider.Position = aabb.Position;
                break;
            case ReeLib.Rcol.ShapeType.Mesh:
                var mcol = shape.As<MeshColliderResource>();
                if (mcol == null) return;
                if (mcol.IsEmpty) {
                    if (AssetConverter.InstanceForGame(mcol.Game).Mcol.ImportSync(mcol)) {
                        ResourceSaver.Save(mcol);
                    }
                }
                if (mcol.CachedVertexCount > 2500) {
                    // don't do preview for large mesh colliders to help with performance
                    collider.Shape = null;
                    return;
                }

                var mesh = mcol.GetMesh();
                collider.Shape = mesh?.CreateTrimeshShape();
                var csgRoot = collider.GetChild(0) as CsgCombiner3D;
                csgRoot?.QueueFreeRemoveChildren();
                var mcolRoot = mcol.Instantiate();
                if (mcolRoot != null && mcolRoot.ColliderRoot != null) {
                    if (csgRoot == null) {
                        csgRoot = new CsgCombiner3D() { CalculateTangents = false, MaterialOverride = EditorResources.McolMaterial };
                        collider.AddChild(csgRoot);
                        csgRoot.AddToGroup(EditorResources.IgnoredSceneGroup);
                    }
                    foreach (var coll in mcolRoot.ColliderRoot.FindChildrenByType<CollisionShape3D>()) {
                        if (coll.Shape is SphereShape3D mcolSphere) {
                            csgRoot.AddChild(new CsgSphere3D() { Position = coll.Position, Radius = mcolSphere.Radius });
                        } else if (coll.Shape is BoxShape3D mcolBox) {
                            csgRoot.AddChild(new CsgBox3D() { Position = coll.Position, Size = mcolBox.Size });
                        } else if (coll.Shape is CapsuleShape3D mcolCapsule) {
                            csgRoot.AddChild(new CsgCylinder3D() { Position = coll.Position, Height = mcolCapsule.Height, Radius = mcolCapsule.Radius });
                        }
                    }
                } else {
                    csgRoot?.QueueFree();
                }
                break;
            case ReeLib.Rcol.ShapeType.HeightField:
                var hf = shape.As<ColliderHeightFieldResource>();
                if (hf.IsEmpty) {
                    if (AssetConverter.InstanceForGame(hf.Game).Chf.ImportSync(hf)) {
                        ResourceSaver.Save(hf);
                    }
                }
                collider.Shape = hf.HeightMap;
                // collider.Position = (hf.MinRange + hf.MaxRange) / 2;
                var span = hf.MaxRange - hf.MinRange;
                if (hf.HeightMap != null) {
                    collider.Scale = new Vector3(
                        hf.TileSize.X,
                        1,
                        hf.TileSize.Y
                    );
                } else {
                    collider.Scale = Vector3.One;
                }
                break;
        }
    }

    public static void ApplyShape(CollisionShape3D collider, ReeLib.via.Sphere sphere)
    {
        if (collider.Shape is not SphereShape3D shape) {
            collider.Shape = shape = new SphereShape3D();
        }
        shape.Radius = sphere.r;
        collider.Position = sphere.pos.ToGodot();
    }

    public static void ApplyShape(CollisionShape3D collider, ReeLib.via.OBB obb)
    {
        if (collider.Shape is not BoxShape3D shape) {
            collider.Shape = shape = new BoxShape3D();
        }
        shape.Size = obb.Extent.ToGodot() * 2;
        collider.Transform = (Transform3D)obb.Coord.ToProjection();
    }

    public static void ApplyShape(CollisionShape3D collider, ReeLib.via.Capsule capsule)
    {
        if (collider.Shape is not CapsuleShape3D shape) {
            collider.Shape = shape = new CapsuleShape3D();
        }
        var p0 = capsule.p0.ToGodot();
        var p1 = capsule.p1.ToGodot();
        // RE format capsule: p0 and p1 are placed at the cylinder center, whereas godot places it at the far end of the capsule, which is why we need to add the radius
        shape.Height = p0.DistanceTo(p1) + capsule.r * 2;
        shape.Radius = capsule.r;
        collider.Position = (p0 + p1) / 2;
        collider.Rotation = (p1 - p0).DirectionToQuaternion(Vector3.Up).GetEuler();
    }

    private static void FixNegativeAabb(ref Aabb aabb)
    {
        var p = aabb.Position;
        var s = aabb.Size;
        if (s.X < 0) { p.X += s.X; s.X = -s.X; }
        if (s.Y < 0) { p.Y += s.Y; s.Y = -s.Y; }
        if (s.Z < 0) { p.Z += s.Z; s.Z = -s.Z; }
        aabb.Position = p;
        aabb.Size = s;
    }

    public static ReeLib.via.Sphere ConvertShapeToRsz(Node3D node, SphereShape3D sphere)
        => new ReeLib.via.Sphere() { pos = node.Position.ToRsz(), R = sphere.Radius };

    public static ReeLib.via.Capsule ConvertShapeToRsz(Node3D node, CapsuleShape3D capsule)
    {
        var cap = new ReeLib.via.Capsule() { r = capsule.Radius };
        var center = node.Position;
        var up = node.Transform.Basis.Y.Normalized();
        cap.p0 = (center - up * (0.5f * capsule.Height - capsule.Radius)).ToRsz();
        cap.p1 = (center + up * (0.5f * capsule.Height - capsule.Radius)).ToRsz();
        return cap;
    }
    public static ReeLib.via.OBB ConvertShapeToRsz(Node3D node, BoxShape3D box)
    {
        var obb = new ReeLib.via.OBB();
        obb.Extent = box.Size.ToRsz();
        obb.Coord = new Projection(node.Transform).ToRsz();
        return obb;
    }
    public static ReeLib.via.AABB ConvertShapeToRszAabb(Node3D node, BoxShape3D box)
    {
        return new Aabb(node.Position, box.Size).ToRsz();
    }

    public static object? ConvertShapeToRsz(Shape3D godotShape, Node3D node, ReeLib.Rcol.ShapeType shapeType, SupportedGame game)
    {
        switch (shapeType) {
            case ReeLib.Rcol.ShapeType.Aabb:
                return ConvertShapeToRszAabb(node, (BoxShape3D)godotShape);
            case ReeLib.Rcol.ShapeType.Box:
                return ConvertShapeToRsz(node, (BoxShape3D)godotShape);
            case ReeLib.Rcol.ShapeType.Sphere:
            case ReeLib.Rcol.ShapeType.ContinuousSphere:
                return ConvertShapeToRsz(node, (SphereShape3D)godotShape);
            case ReeLib.Rcol.ShapeType.Capsule:
            case ReeLib.Rcol.ShapeType.ContinuousCapsule:
                return ConvertShapeToRsz(node, (CapsuleShape3D)godotShape);
            case ReeLib.Rcol.ShapeType.HeightField:
                // nothing to do - show default error in case of rcol calls since those probably don't support CHF
            default:
                GD.PrintErr("Unsupported collider type " + shapeType);
                return null;
        }
    }

    public static void UpdateSerializedShape(REObject obj, REFieldAccessor accessor, Shape3D godotShape, Node3D node, ReeLib.Rcol.ShapeType shapeType)
    {
        switch (shapeType) {
            case ReeLib.Rcol.ShapeType.Aabb:
                var box = (BoxShape3D)godotShape;
                obj.SetField(accessor, new Aabb(node.Position, box.Size));
                break;
            case ReeLib.Rcol.ShapeType.Box:
                var obb = obj.GetField(accessor).As<OrientedBoundingBox>();
                var box2 = (BoxShape3D)godotShape;
                obb.extent = box2.Size / 2;
                obb.coord = new Projection(node.Transform);
                obj.SetField(accessor, obb);
                break;
            case ReeLib.Rcol.ShapeType.Sphere:
            case ReeLib.Rcol.ShapeType.ContinuousSphere:
                var sphere = (SphereShape3D)godotShape;
                var pos = node.Position;
                obj.SetField(accessor, new Vector4(pos.X, pos.Y, pos.Z, sphere.Radius));
                break;
            case ReeLib.Rcol.ShapeType.Capsule:
            case ReeLib.Rcol.ShapeType.ContinuousCapsule:
                var capsule = (CapsuleShape3D)godotShape;
                var cap = obj.GetField(accessor).As<Capsule>();
                cap.r = capsule.Radius;
                var cappos = node.Position;
                var up = node.Transform.Basis.Y.Normalized();
                cap.p0 = cappos - up * (0.5f * capsule.Height - capsule.Radius);
                cap.p1 = cappos + up * (0.5f * capsule.Height - capsule.Radius);
                obj.SetField(accessor, cap);
                break;
            case ReeLib.Rcol.ShapeType.HeightField:
                var path = godotShape.ResourcePath;
                if (string.IsNullOrEmpty(path)) {
                    // do nothing, keep the value as is
                    // the assumption is that we probably don't want null shapes, the user should just remove the collider instead
                    GD.PrintErr($"HeightField shape with pathless height map shape is not supported. Please assign the {nameof(ColliderHeightFieldResource.HeightMap)} from a {nameof(ColliderHeightFieldResource)}.");
                } else {
                    var pathSplit = path.IndexOf("::");
                    if (pathSplit != -1) {
                        var hfPath = path[..pathSplit];
                        if (ResourceLoader.Exists(hfPath)) {
                            obj.SetField(accessor, ResourceLoader.Load<ColliderHeightFieldResource>(hfPath));
                        }
                    } else {
                        GD.PrintErr($"HeightField shape's {nameof(ColliderHeightFieldResource)} path could not be determined. Please assign the {nameof(ColliderHeightFieldResource.HeightMap)} from a {nameof(ColliderHeightFieldResource)}.");
                    }
                }
                break;
            default:
                GD.PrintErr("Unsupported collider type " + shapeType);
                break;
        }
    }

    public static RszFieldType GetShapeFieldType(ReeLib.Rcol.ShapeType shapeType)
    {
        return shapeType switch {
            ReeLib.Rcol.ShapeType.Aabb => RszFieldType.AABB,
            ReeLib.Rcol.ShapeType.Sphere => RszFieldType.Sphere,
            ReeLib.Rcol.ShapeType.Capsule => RszFieldType.Capsule,
            ReeLib.Rcol.ShapeType.Box => RszFieldType.OBB,
            ReeLib.Rcol.ShapeType.Area => RszFieldType.Area,
            // ReeLib.Rcol.ShapeType.Triangle => handler.Read<via.Triangle>(),
            ReeLib.Rcol.ShapeType.Cylinder => RszFieldType.Cylinder,
            ReeLib.Rcol.ShapeType.ContinuousSphere => RszFieldType.Sphere,
            ReeLib.Rcol.ShapeType.ContinuousCapsule => RszFieldType.Capsule,
            _ => RszFieldType.ukn_type,
        };
    }
}
