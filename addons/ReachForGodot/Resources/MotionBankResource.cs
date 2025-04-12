namespace ReaGE;

using Godot;

[GlobalClass, Tool, ResourceHolder("motbank", SupportedFileFormats.MotionBank)]
public partial class MotionBankResource : REResource, IImportableAsset, IExportableAsset
{
    public MotionBankResource() : base(SupportedFileFormats.MotionBank)
    {
    }

    [Export] public UvarResource? Uvar { get; set; }
    [Export] public MotionBankEntry[]? MotionList { get; set; }

    public bool IsEmpty => Uvar == null && !(MotionList?.Length > 0);
}
