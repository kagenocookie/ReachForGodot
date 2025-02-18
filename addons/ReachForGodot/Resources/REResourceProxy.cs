namespace RGE;

using System;
using Godot;
using RszTool;

/// <summary>
/// Proxy placeholder resource for resources that have a non-REResource compatible representation in engine (e.g. meshes)
/// </summary>
[GlobalClass, Tool]
public partial class REResourceProxy : REResource
{
    [Export] public string? ImportedPath { get; set; }
    [Export] public Resource? ImportedResource { get; set; }
}
