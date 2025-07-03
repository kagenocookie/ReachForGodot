namespace ReaGE.Physics;

using Godot;

[GlobalClass, Tool]
public partial class RszCollisionShape3D : CollisionShape3D
{
    [Export] public int ColliderIndex { get; set; }
    [Export] public MeshColliderResource? MeshCollider { get; set; }

    [ExportToolButton("Refresh mesh shape")]
    private Callable BtnRefreshShape => Callable.From(RefreshShape);

    private void RefreshShape()
    {
        var gameobject = this.FindNodeInParents<GameObject>();
        if (gameobject == null) return;

        var coll = gameobject.GetComponent<PhysicsCollidersComponent>();
        if (coll == null) return;

        if (MeshCollider != null) {
            RequestSetCollisionShape3D.ApplyShape(this, ReeLib.Rcol.ShapeType.Mesh, MeshCollider);
        }
    }
}