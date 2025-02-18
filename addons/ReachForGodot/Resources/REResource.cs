namespace RFG;

using System;
using Godot;
using RszTool;

[GlobalClass, Tool]
public partial class REResource : REObject
{
    [Export] public AssetReference? Asset { get; set; }
    /// <summary>Path to an imported asset file pointed to by this resource. Null if this itself is the resource.</summary>
    [Export] public string? ImportedPath { get; set; }
    /// <summary>The imported asset resource file pointed to by this resource. Null if this itself is the resource.</summary>
    [Export] public Resource? ImportedResource { get; set; }
    [Export] public RESupportedFileFormats ResourceType { get; set; } = RESupportedFileFormats.Unknown;

    [ExportToolButton("Show source file")]
    private Callable OpenSourceFile => Callable.From(() => Asset?.OpenSourceFile(Game));
}
