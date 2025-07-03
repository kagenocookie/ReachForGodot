namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using ReeLib;

[GlobalClass, Tool, REComponentClass("via.Transform")]
public partial class RETransformComponent : REComponent
{
    public RETransformComponent()
    {
    }

    public RETransformComponent(SupportedGame game, string classname) : base(game, classname)
    {
    }

    public override Task Setup(RszImportType importType)
    {
        var quat = GetField(1).VariantToQuaternion();
        var scale = GetField(2).VariantToVector3();
        GameObject.Transform = new Transform3D(
            new Basis(quat).Scaled(scale),
            GetField(0).VariantToVector3()
        );
        return Task.CompletedTask;
    }

    public static void ApplyTransform(Node3D node, RszInstance rsz)
    {
        node.Transform = Vector4x3ToTransform(
            ObjectToVector3(rsz.Values[0]),
            ObjectToQuaternion(rsz.Values[1]),
            ObjectToVector3(rsz.Values[2])
        );
    }

    private static Quaternion ObjectToQuaternion(object? quat)
        => quat is System.Numerics.Vector4 v4 ? v4.ToGodot().ToQuaternion() :
            quat is System.Numerics.Quaternion qq ? qq.ToGodot()
            : Quaternion.Identity;

    private static Vector3 ObjectToVector3(object? vec)
        => vec is System.Numerics.Vector4 v4 ? v4.ToGodot().ToVector3() :
            vec is System.Numerics.Vector3 qq ? qq.ToGodot()
            : default;

    public static Transform3D Vector4x3ToTransform(Vector3 pos, Quaternion rotation, Vector3 scale)
        => new Transform3D(new Basis(rotation).Scaled(scale), pos);

    public override void PreExport()
    {
        base.PreExport();
        Debug.Assert(GameObject?.Transform != null);
        var transform = GameObject.Transform;
        var basis = transform.Basis;
        SetField(TypeInfo.Fields[0], transform.Origin.ToVector4());
        SetField(TypeInfo.Fields[1], TypeInfo.Fields[1].RszField.type == RszFieldType.Quaternion
            ? basis.GetRotationQuaternion()
            : basis.GetRotationQuaternion().ToVector4());
        SetField(TypeInfo.Fields[2], basis.Scale.ToVector4());
    }

    public static Transform3D Vector4x3ToTransform(System.Numerics.Vector4 pos, System.Numerics.Vector4 rotation, System.Numerics.Vector4 scale)
    {
        var row1 = (System.Numerics.Vector4)pos;
        var row2 = (System.Numerics.Vector4)rotation;
        var row3 = (System.Numerics.Vector4)scale;
        return new Transform3D(
            new Basis(new Quaternion(row2.X, row2.Y, row2.Z, row2.W))
                .Scaled(new Vector3(row3.X, row3.Y, row3.Z)),
            new Vector3(row1.X, row1.Y, row1.Z)
        );
    }
}