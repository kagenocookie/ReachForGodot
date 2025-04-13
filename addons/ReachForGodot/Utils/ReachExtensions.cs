using Godot;

namespace ReaGE;

public static class ReachExtensions
{
    public static IRszContainer? FindRszOwner(this Node node)
    {
        if (node is IRszContainer rsz && rsz.Asset?.IsEmpty == false) {
            return rsz;
        }
        // ignore the owner node and check the hierarchy directly
        // this is to correctly handle objects added to EditableInstance nodes
        return node.FindNodeInParents<IRszContainer>(p => p.Asset?.IsEmpty == false)
            ?? node as IRszContainer
            ?? node.FindNodeInParents<IRszContainer>();
    }

    public static Node? FindRszOwnerNode(this Node node)
    {
        return FindRszOwner(node) as Node;
    }
}