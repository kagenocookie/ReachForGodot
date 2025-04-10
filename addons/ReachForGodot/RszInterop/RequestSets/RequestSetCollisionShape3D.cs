namespace ReaGE;

using System;
using Godot;
using Godot.Collections;
using RszTool;

[GlobalClass, Tool]
public partial class RequestSetCollisionShape3D : CollisionShape3D
{
    [Export] public RszTool.Rcol.ShapeType RcolShapeType { get; set; }
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

    public RszTool.Rcol.RcolShape ToRsz(SupportedGame game)
    {
        var shape = new RszTool.Rcol.RcolShape();
        shape.Info.Guid = Guid;
        shape.Info.Name = OriginalName ?? string.Empty;
        shape.Info.primaryJointNameStr = PrimaryJointNameStr ?? string.Empty;
        shape.Info.secondaryJointNameStr = SecondaryJointNameStr ?? string.Empty;
        shape.Info.LayerIndex = LayerIndex;
        shape.Info.SkipIdBits = SkipIdBits;
        shape.Info.IgnoreTagBits = IgnoreTagBits;
        shape.Info.Attribute = Attribute;
        shape.Info.shapeType = RcolShapeType;
        shape.shape = RequestSetCollisionShape3D.Shape3DToRszShape(Shape, this, RcolShapeType, game);
        return shape;
    }

    public override string ToString() => Name;

    public static void ApplyShape(CollisionShape3D collider, RszTool.Rcol.ShapeType shapeType, Variant shape)
    {
        switch (shapeType) {
            case RszTool.Rcol.ShapeType.Sphere:
            case RszTool.Rcol.ShapeType.ContinuousSphere:
                var sphere = shape.AsVector4();
                collider.Shape = new SphereShape3D() { Radius = sphere.W };
                collider.Position = sphere.ToVector3();
                break;
            case RszTool.Rcol.ShapeType.Box:
                var obb = shape.As<OrientedBoundingBox>();
                // TODO: sometime the obb extents are all == 0. should we use the BoundingAabb for size instead?
                obb.extent.X = Mathf.Max(0.001f, Mathf.Abs(obb.extent.X));
                obb.extent.Y = Mathf.Max(0.001f, Mathf.Abs(obb.extent.Y));
                obb.extent.Z = Mathf.Max(0.001f, Mathf.Abs(obb.extent.Z));
                collider.Shape = new BoxShape3D() { Size = obb.extent };
                collider.Transform = (Transform3D)obb.coord;
                break;
            case RszTool.Rcol.ShapeType.Capsule:
            case RszTool.Rcol.ShapeType.ContinuousCapsule:
                var capsule = shape.As<Capsule>();
                // RE format capsule: p0 and p1 are placed at the cylinder center, whereas godot places it at the far end of the capsule, which is why we need to add the radius
                collider.Shape = new CapsuleShape3D() { Height = capsule.p0.DistanceTo(capsule.p1) + capsule.r * 2, Radius = capsule.r };
                collider.Position = (capsule.p0 + capsule.p1) / 2;
                collider.Rotation = (capsule.p1 - capsule.p0).DirectionToQuaternion(Vector3.Up).GetEuler();
                break;
            case RszTool.Rcol.ShapeType.Aabb:
                var aabb = shape.As<Aabb>();
                FixNegativeAabb(ref aabb);
                if (aabb.Size.X < 0) { aabb.Position = new Vector3(aabb.Position.X + aabb.Size.X, aabb.Position.Y, aabb.Position.Z); }
                collider.Shape = new BoxShape3D() { Size = aabb.Size };
                collider.Position = aabb.Position;
                break;
        }
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

    public static object? Shape3DToRszShape(Shape3D godotShape, Node3D node, RszTool.Rcol.ShapeType shapeType, SupportedGame game)
    {
        switch (shapeType) {
            case RszTool.Rcol.ShapeType.Aabb:
                var box = (BoxShape3D)godotShape;
                return new Aabb(node.Position, box.Size).ToRsz();
            case RszTool.Rcol.ShapeType.Box:
                var obb = new RszTool.via.OBB();
                var box2 = (BoxShape3D)godotShape;
                obb.Extent = box2.Size.ToRsz();
                obb.Coord = new Projection(node.Transform).ToRsz();
                return obb;
            case RszTool.Rcol.ShapeType.Sphere:
            case RszTool.Rcol.ShapeType.ContinuousSphere:
                var sphere = (SphereShape3D)godotShape;
                return new RszTool.via.Sphere() { pos = node.Position.ToRsz(), R = sphere.Radius };
            case RszTool.Rcol.ShapeType.Capsule:
            case RszTool.Rcol.ShapeType.ContinuousCapsule:
                var capsule = (CapsuleShape3D)godotShape;
                var cap = new RszTool.via.Capsule() { r = capsule.Radius };
                var center = node.Position;
                var up = node.Transform.Basis.Y.Normalized();
                cap.p0 = (center - up * (0.5f * capsule.Height - capsule.Radius)).ToRsz();
                cap.p1 = (center + up * (0.5f * capsule.Height - capsule.Radius)).ToRsz();
                return cap;
            default:
                GD.PrintErr("Unsupported collider type " + shapeType);
                return null;
        }
    }

    public static void UpdateSerializedShape(REObject obj, REFieldAccessor accessor, Shape3D godotShape, Node3D node, RszTool.Rcol.ShapeType shapeType)
    {
        switch (shapeType) {
            case RszTool.Rcol.ShapeType.Aabb:
                var box = (BoxShape3D)godotShape;
                obj.SetField(accessor, new Aabb(node.Position, box.Size));
                break;
            case RszTool.Rcol.ShapeType.Box:
                var obb = obj.GetField(accessor).As<OrientedBoundingBox>();
                var box2 = (BoxShape3D)godotShape;
                obb.extent = box2.Size;
                obb.coord = new Projection(node.Transform);
                obj.SetField(accessor, obb);
                break;
            case RszTool.Rcol.ShapeType.Sphere:
            case RszTool.Rcol.ShapeType.ContinuousSphere:
                var sphere = (SphereShape3D)godotShape;
                var pos = node.Position;
                obj.SetField(accessor, new Vector4(pos.X, pos.Y, pos.Z, sphere.Radius));
                break;
            case RszTool.Rcol.ShapeType.Capsule:
            case RszTool.Rcol.ShapeType.ContinuousCapsule:
                var capsule = (CapsuleShape3D)godotShape;
                var cap = obj.GetField(accessor).As<Capsule>();
                cap.r = capsule.Radius;
                var cappos = node.Position;
                var up = node.Transform.Basis.Y.Normalized();
                cap.p0 = cappos - up * (0.5f * capsule.Height - capsule.Radius);
                cap.p1 = cappos + up * (0.5f * capsule.Height - capsule.Radius);
                obj.SetField(accessor, cap);
                break;
            default:
                GD.PrintErr("Unsupported collider type " + shapeType);
                break;
        }
    }

    public static RszFieldType GetShapeFieldType(RszTool.Rcol.ShapeType shapeType)
    {
        return shapeType switch {
            RszTool.Rcol.ShapeType.Aabb => RszFieldType.AABB,
            RszTool.Rcol.ShapeType.Sphere => RszFieldType.Sphere,
            RszTool.Rcol.ShapeType.Capsule => RszFieldType.Capsule,
            RszTool.Rcol.ShapeType.Box => RszFieldType.OBB,
            RszTool.Rcol.ShapeType.Area => RszFieldType.Area,
            // RszTool.Rcol.ShapeType.Triangle => handler.Read<via.Triangle>(),
            RszTool.Rcol.ShapeType.Cylinder => RszFieldType.Cylinder,
            RszTool.Rcol.ShapeType.ContinuousSphere => RszFieldType.Sphere,
            RszTool.Rcol.ShapeType.ContinuousCapsule => RszFieldType.Capsule,
            _ => RszFieldType.ukn_type,
        };
    }
}
