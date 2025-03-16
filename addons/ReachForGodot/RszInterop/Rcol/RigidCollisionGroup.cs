namespace ReaGE;

using System;
using Godot;

[GlobalClass, Tool]
public partial class RigidCollisionGroup : AnimatableBody3D
{
    [Export] private string? uuidString;

    public Guid Guid {
        get => Guid.TryParse(uuidString, out var guid) ? guid : Guid.Empty;
        set => uuidString = value.ToString();
    }
}
