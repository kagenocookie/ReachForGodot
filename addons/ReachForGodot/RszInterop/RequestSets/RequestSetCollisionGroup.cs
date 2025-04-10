namespace ReaGE;

using System;
using Godot;
using RszTool;

[GlobalClass, Tool, FieldAccessorProvider]
public partial class RequestSetCollisionGroup : AnimatableBody3D
{
    [Export] private string? uuidString;
    [Export] private string? layerGuid;
    [Export] public string[]? MaskGuids { get; set; }
    [Export] public REObject? Data { get; set; }

    [REObjectFieldTarget("via.physics.RequestSetCollider.RequestSetGroup")]
    private static readonly REFieldAccessor RcolGroupField = new REFieldAccessor("File")
        .Resource<RcolResource>()
        .Conditions(list => list.FirstOrDefault(f => f.RszField.type is RszFieldType.String or RszFieldType.Resource));

    public IEnumerable<RequestSetCollisionShape3D> Shapes => this.FindChildrenByType<RequestSetCollisionShape3D>();

    public Guid Guid {
        get => Guid.TryParse(uuidString, out var guid) ? guid : Guid.Empty;
        set => uuidString = value.ToString();
    }

    public Guid LayerGuid {
        get => Guid.TryParse(layerGuid, out var guid) ? guid : Guid.Empty;
        set => layerGuid = value.ToString();
    }

    public RszTool.Rcol.RcolGroup ToRsz()
    {
        var group = new RszTool.Rcol.RcolGroup();
        group.Info.guid = Guid;
        group.Info.Name = Name;
        group.Info.MaskBits = CollisionMask;
        group.Info.MaskGuids = MaskGuids?.Select(c => Guid.Parse(c)).ToList() ?? new();
        group.Info.LayerGuid = LayerGuid;
        return group;
    }

    public override string ToString() => Name;
}
