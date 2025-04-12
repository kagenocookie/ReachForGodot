namespace ReaGE;

using Godot;

[GlobalClass, Tool]
public partial class MotionBankEntry : Resource
{
    [Export] public MotionListResource? Motion { get; set; }
    [Export] public int BankID { get; set; }
    [Export] public uint BankType { get; set; }
    [Export] public uint BankTypeMaskBits { get; set; }
}
