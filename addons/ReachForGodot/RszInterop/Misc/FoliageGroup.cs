namespace ReaGE;

using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool]
public partial class FoliageGroup : Resource
{
    [Export] public Godot.Collections.Array<Transform3D>? Transforms;
    [Export] public MeshResource? Mesh;
    [Export] public MaterialResource? Material;
    [Export] public Aabb Bounds;
}
