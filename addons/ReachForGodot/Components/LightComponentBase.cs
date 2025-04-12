namespace ReaGE;

using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool]
public abstract partial class LightComponentBase : REComponent
{
    protected Light3D? lightNode;

    public override void PreExport()
    {
        if (lightNode != null && IsInstanceValid(lightNode) && !lightNode.Transform.IsEqualApprox(Transform3D.Identity)) {
            GD.PrintErr("Detected movement in mesh component - move the parent GameObject instead: " + Path);
            lightNode.SetIdentity();
        }
    }

    public async Task<TLightType> FindOrCreateLightNode<TLightType>() where TLightType : Light3D, new()
    {
        if (lightNode != null && !IsInstanceValid(lightNode)) {
            lightNode = null;
        }
        lightNode ??= GameObject.FindChildWhere<Light3D>(child => child.GetType() == typeof(Light3D) && child.Name.ToString().StartsWith("__"));
        if (!IsInstanceValid(lightNode)) {
            lightNode = null;
        }
        if (lightNode == null || lightNode is not TLightType targetLightType) {
            lightNode?.GetParent().RemoveChild(lightNode);
            lightNode?.QueueFree();
            lightNode = targetLightType = new TLightType() { Name = "__light" };
            await GameObject.AddChildAsync(lightNode, GameObject.FindRszOwnerNode());
        }

        return targetLightType;
    }
}