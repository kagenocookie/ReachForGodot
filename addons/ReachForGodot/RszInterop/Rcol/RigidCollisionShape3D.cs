namespace ReaGE;

using System;
using Godot;
using RszTool;

[GlobalClass, Tool]
public partial class RigidCollisionShape3D : CollisionShape3D
{
    [Export] public RszTool.RcolFile.ShapeType RcolShapeType { get; set; }
    [Export] public string? Uuid { get; private set; }
    [Export] public string? OriginalName { get; set; }

    [Export] public int LayerIndex { get; set; }
    [Export] public uint SkipIdBits { get; set; }
    [Export] public uint IgnoreTagBits { get; set; }
    [Export] public int Attribute { get; set; }
    [Export] public string? PrimaryJointNameStr { get; set; }
    [Export] public string? SecondaryJointNameStr { get; set; }

    [Export] public REObject? Data { get; set; }

    public Guid Guid {
        get => Guid.TryParse(Uuid, out var guid) ? guid : Guid.Empty;
        set => Uuid = value.ToString();
    }

    public static void ApplyShape(CollisionShape3D collider, RszTool.RcolFile.ShapeType shapeType, Variant shape)
    {
        switch (shapeType) {
            case RszTool.RcolFile.ShapeType.Sphere:
            case RszTool.RcolFile.ShapeType.ContinuousSphere:
                var sphere = shape.AsVector4();
                collider.Shape = new SphereShape3D() { Radius = sphere.W };
                collider.Position = sphere.ToVector3();
                break;
            case RszTool.RcolFile.ShapeType.Box:
                var obb = shape.As<OrientedBoundingBox>();
                // TODO: sometime the obb extents are all == 0. should we use the BoundingAabb for size instead?
                obb.extent.X = Mathf.Max(0.001f, Mathf.Abs(obb.extent.X));
                obb.extent.Y = Mathf.Max(0.001f, Mathf.Abs(obb.extent.Y));
                obb.extent.Z = Mathf.Max(0.001f, Mathf.Abs(obb.extent.Z));
                collider.Shape = new BoxShape3D() { Size = obb.extent };
                collider.Transform = (Transform3D)obb.coord;
                break;
            case RszTool.RcolFile.ShapeType.Capsule:
            case RszTool.RcolFile.ShapeType.ContinuousCapsule:
                var capsule = shape.As<Capsule>();
                collider.Shape = new CapsuleShape3D() { Height = capsule.p0.DistanceTo(capsule.p1), Radius = capsule.r };
                collider.Position = (capsule.p0 + capsule.p1) / 2;
                break;
            case RszTool.RcolFile.ShapeType.Aabb:
                var aabb = shape.As<Aabb>();
                collider.Shape = new BoxShape3D() { Size = aabb.Size };
                collider.Position = aabb.Position;
                break;
        }
    }

    public static RszFieldType GetShapeFieldType(RcolFile.ShapeType shapeType)
    {
        return shapeType switch {
            RcolFile.ShapeType.Aabb => RszFieldType.AABB,
            RcolFile.ShapeType.Sphere => RszFieldType.Sphere,
            RcolFile.ShapeType.Capsule => RszFieldType.Capsule,
            RcolFile.ShapeType.Box => RszFieldType.OBB,
            RcolFile.ShapeType.Area => RszFieldType.Area,
            // RcolFile.ShapeType.Triangle => handler.Read<via.Triangle>(),
            RcolFile.ShapeType.Cylinder => RszFieldType.Cylinder,
            RcolFile.ShapeType.ContinuousSphere => RszFieldType.Sphere,
            RcolFile.ShapeType.ContinuousCapsule => RszFieldType.Capsule,
            _ => RszFieldType.ukn_type,
        };
    }
}
