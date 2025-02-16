#nullable enable

using System;
using Godot;

namespace RFG;

[GlobalClass, Tool]
public partial class AssetReference : Resource
{
    [Export] public string AssetFilename { get; set; } = string.Empty;
}