namespace ReaGE;

using System;
using Godot;
using RszTool;

[GlobalClass, Tool]
public partial class RequestSetCollisionGroup : AnimatableBody3D
{
    [Export] private string? uuidString;
    [Export] private string? layerGuid;
    [Export] public string[]? MaskGuids { get; set; }
    [Export] public REObject? Data { get; set; }

    public IEnumerable<RequestSetCollisionShape3D> Shapes => this.FindChildrenByType<RequestSetCollisionShape3D>();

    public Guid Guid {
        get => Guid.TryParse(uuidString, out var guid) ? guid : Guid.Empty;
        set => uuidString = value.ToString();
    }

    public Guid LayerGuid {
        get => Guid.TryParse(layerGuid, out var guid) ? guid : Guid.Empty;
        set => layerGuid = value.ToString();
    }

    public RcolFile.RcolGroup ToRsz()
    {
        var group = new RcolFile.RcolGroup();
        group.Info.guid = Guid;
        group.Info.name = Name;
        group.Info.MaskBits = CollisionMask;
        group.Info.MaskGuids = MaskGuids?.Select(c => Guid.Parse(c)).ToArray() ?? Array.Empty<Guid>();
        group.Info.LayerGuid = LayerGuid;
        return group;
    }

    public override string ToString() => Name;
}
