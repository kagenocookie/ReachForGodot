namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class CollisionAttributeDefinition : Resource
{
    [Export] public string? Name { get; set; }
    [Export] public string? guidString { get; set; }
    public Guid Guid
    {
        get => Guid.TryParse(guidString, out var guid) ? guid : Guid.Empty;
        set => guidString = value.ToString();
    }
}
