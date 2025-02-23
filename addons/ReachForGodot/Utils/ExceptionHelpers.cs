namespace RGE;

using Godot;
using RszTool;

public static class ExceptionHelpers
{
    public static void LogRszRetryException(this RszRetryOpenException exception)
    {
        // TODO automate?
        GD.PrintErr("Retrying rsz open operation. Consider noting this change down in the rsz patch files to make it consistently correct in the future:\n" + exception.Message);
    }
}
