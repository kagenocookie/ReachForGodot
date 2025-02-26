namespace RGE;

using System;
using Godot;

[GlobalClass, Tool]
public partial class GameobjectEditorPlaceholder : Node
{
    [Export] public Godot.Collections.Array<REComponent> Components { get; set; } = null!;
    [Export] public REComponent Component { get; set; } = null!;
}