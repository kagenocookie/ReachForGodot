namespace RGE;

using System;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using RszTool;

[GlobalClass, Tool]
public abstract partial class REComponent : Node
{
    [Export] public int ObjectId = -1;
    [Export] public REObject? Data { get; set; }

    public string? Classname => Data?.Classname;
    public REGameObject? GameObject => this.FindNodeInParents<REGameObject>();

    public abstract Task Setup(IRszContainerNode root, REGameObject gameObject, RszInstance rsz);

    public IEnumerable<IRszContainerNode> SerializedContainers => this.FindParentsByType<IRszContainerNode>();
    public T? FindResource<T>(string filepath) where T : REResource => SerializedContainers.Select(sc => sc.FindResource<T>(filepath)).FirstOrDefault();
}
