namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, Tool, REComponentClass("via.landscape.Foliage")]
public partial class Foliage : REComponent
{
    private static readonly REFieldAccessor FoliageResourceField = new REFieldAccessor("FoliageData")
        .Resource<FoliageResource>()
        .Conditions(l => l.FirstOrDefault(f => f.RszField.type is RszFieldType.String or RszFieldType.Resource));

    [ExportToolButton("Re-setup foliage meshes")]
    private Callable Resetup => Callable.From(() => { _ = Setup(RszImportType.Reimport); });

    public override async Task Setup(RszImportType importType)
    {
        if (importType == RszImportType.Placeholders) return;
        var foliage = GetField(FoliageResourceField).As<FoliageResource>();
        if (foliage == null) return;

        await foliage.EnsureImported(true);
        if (foliage.Groups == null) return;

        GameObject.QueueFreeRemoveChildren();

        int childCount = 0;
        foreach (var group in foliage.Groups) {
            if (group.Mesh == null) continue;

            await group.Mesh.Import(false);

            var mesh = new MultiMeshInstance3D() { Name = "mesh_" + childCount++ };

            if (group.Mesh.ImportedResource is PackedScene scene && scene.Instantiate<Node3D>(PackedScene.GenEditState.Instance).FindChildByTypeRecursive<MeshInstance3D>() is MeshInstance3D meshinst) {
                var mm = new MultiMesh();
                mm.InstanceCount = 0;
                mm.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
                mm.Mesh = meshinst.Mesh;
                if (group.Transforms != null) {
                    mm.InstanceCount = group.Transforms.Count;
                    for (int i = 0; i < group.Transforms.Count; ++i) {
                        mm.SetInstanceTransform(i, group.Transforms[i]);
                    }
                }
                mesh.Multimesh = mm;
            }

            await GameObject.AddChildAsync(mesh, GameObject.FindRszOwnerNode());
        }
    }
}