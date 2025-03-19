namespace ReaGE;

using System;
using Godot;

[GlobalClass, Tool]
public partial class RequestSetCollisionGroup : AnimatableBody3D
{
    [Export] private string? uuidString;
    [Export] private string? layerGuid;
    [Export] public string[]? MaskGuids { get; set; }

    public Guid Guid {
        get => Guid.TryParse(uuidString, out var guid) ? guid : Guid.Empty;
        set => uuidString = value.ToString();
    }

    public Guid LayerGuid {
        get => Guid.TryParse(layerGuid, out var guid) ? guid : Guid.Empty;
        set => layerGuid = value.ToString();
    }
}
