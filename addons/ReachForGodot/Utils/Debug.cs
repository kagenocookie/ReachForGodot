namespace ReaGE;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Godot;

public static partial class Debug
{
    public static void Assert([DoesNotReturnIf(false)] bool condition, string? msg = null, [CallerArgumentExpression(nameof(condition))] string? conditionString = null)
    {
#if DEBUG
        if (condition) return;

        GD.PrintErr($"Assertion failed: {msg} [{conditionString}]");
        GD.PushError($"Assertion failed: {msg} [{conditionString}]");
        throw new ApplicationException($"Assertion failed: {msg} [{conditionString}]");
#endif
    }
    public static void AssertIf(bool guard, [DoesNotReturnIf(false)] bool condition, string? msg = null, [CallerArgumentExpression(nameof(condition))] string? conditionString = null)
    {
#if DEBUG
        if (!guard || condition) return;

        GD.PrintErr($"Assertion failed: {msg} [{conditionString}]");
        GD.PushError($"Assertion failed: {msg} [{conditionString}]");
        throw new ApplicationException($"Assertion failed: {msg} [{conditionString}]");
#endif
    }

    public static void Break()
    {
    }
}
