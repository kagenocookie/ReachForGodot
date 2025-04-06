namespace ReaGE;

using Godot;

[GlobalClass, Tool, ResourceHolder("mcol", SupportedFileFormats.MeshCollider)]
public partial class MeshColliderResource : REResource
{
    public MeshColliderResource() : base(SupportedFileFormats.MeshCollider)
    {
    }
}
