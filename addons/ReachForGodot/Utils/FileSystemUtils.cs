using System.Diagnostics;
using Godot;

namespace ReaGE;

public static class FileSystemUtils
{
    public static void ShowFileInExplorer(string? file)
    {
        if (File.Exists(file)) {
            GD.Print("Filename: " + file);
            Process.Start(new ProcessStartInfo("explorer.exe") {
                UseShellExecute = false,
                Arguments = $"/select, \"{file.Replace('/', '\\')}\"",
            });
        } else {
            GD.PrintErr("File not found: " + file);
        }
    }
}