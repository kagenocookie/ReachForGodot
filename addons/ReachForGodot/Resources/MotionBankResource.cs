namespace ReaGE;

using Godot;

[GlobalClass, Tool, ResourceHolder("motbank", RESupportedFileFormats.MotionBank)]
public partial class MotionBankResource : REResource
{
    public MotionBankResource() : base(RESupportedFileFormats.MotionBank)
    {
    }
}
