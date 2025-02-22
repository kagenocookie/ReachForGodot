namespace RGE;

using System;
using Godot;
using RszTool;

[GlobalClass, Tool]
public partial class REResource : REObject
{
    [Export] public AssetReference? Asset { get; set; }
    [Export] public RESupportedFileFormats ResourceType { get; set; } = RESupportedFileFormats.Unknown;
}
