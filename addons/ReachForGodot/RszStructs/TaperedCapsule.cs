namespace RGE;

using Godot;

public partial class TaperedCapsule : Resource
{
    [Export] public Vector4 vertexRadiusA;
    [Export] public Vector4 vertexRadiusB;

    public static implicit operator TaperedCapsule(RszTool.via.TaperedCapsule rszValue) => new TaperedCapsule() {
        vertexRadiusA = rszValue.VertexRadiusA.ToGodot(),
        vertexRadiusB = rszValue.VertexRadiusB.ToGodot()
    };

    public RszTool.via.TaperedCapsule ToRsz() => new() {
        VertexRadiusA = vertexRadiusA.ToRsz(),
        VertexRadiusB = vertexRadiusB.ToRsz(),
    };
}
