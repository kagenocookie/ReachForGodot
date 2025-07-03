namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class TaperedCapsule : Resource
{
    [Export] public Vector4 vertexRadiusA;
    [Export] public Vector4 vertexRadiusB;

    public static implicit operator TaperedCapsule(ReeLib.via.TaperedCapsule rszValue) => new TaperedCapsule() {
        vertexRadiusA = rszValue.VertexRadiusA.ToGodot(),
        vertexRadiusB = rszValue.VertexRadiusB.ToGodot()
    };

    public ReeLib.via.TaperedCapsule ToRsz() => new() {
        VertexRadiusA = vertexRadiusA.ToRsz(),
        VertexRadiusB = vertexRadiusB.ToRsz(),
    };
}
