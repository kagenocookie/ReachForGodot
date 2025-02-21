namespace RGE;

using System;
using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, Tool]
public partial class REComponentPlaceholder : REComponent
{
    public override Task Setup(IRszContainerNode root, REGameObject gameObject, RszInstance rsz) => Task.CompletedTask;
}
