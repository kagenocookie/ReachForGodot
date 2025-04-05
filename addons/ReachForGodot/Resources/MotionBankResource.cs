namespace ReaGE;

using Godot;

[GlobalClass, Tool, ResourceHolder("motbank", SupportedFileFormats.MotionBank)]
public partial class MotionBankResource : REResource
{
    public MotionBankResource() : base(SupportedFileFormats.MotionBank)
    {
    }
}
