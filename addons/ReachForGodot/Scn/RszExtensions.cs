namespace RFG;

using System;
using Godot;
using RszTool;

public static class RszExtensions
{
    public static string? GetStringField(this RszClass cls, string name)
    {
        if (cls.GetField(name) is RszField field && field.type == RszFieldType.String) {

            // return field.type == RszFieldType.String ? field.
        }
        return null;
    }
}