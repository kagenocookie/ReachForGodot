namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class FoliageGroup : Resource
{
    [Export] public Godot.Collections.Array<Transform3D>? Transforms;
    [Export] public MeshResource? Mesh;
    [Export] public MaterialDefinitionResource? Material;
    [Export] public Aabb Bounds;
}
