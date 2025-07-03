namespace ReaGE;

using Godot;
using ReeLib;

[GlobalClass, Tool, ResourceHolder("motbank", KnownFileFormats.MotionBank)]
public partial class MotionBankResource : REResource, IImportableAsset, IExportableAsset
{
    public MotionBankResource() : base(KnownFileFormats.MotionBank)
    {
    }

    [Export] public UvarResource? Uvar { get; set; }
    [Export] public MotionBankEntry[]? MotionList { get; set; }

    public bool IsEmpty => Uvar == null && !(MotionList?.Length > 0);
}
