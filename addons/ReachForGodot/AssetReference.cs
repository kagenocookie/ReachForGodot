#nullable enable

using System;
using System.Diagnostics;
using Godot;

namespace RGE;

[GlobalClass, Tool]
public partial class AssetReference : Resource
{
    public AssetReference()
    {
    }

    public AssetReference(string assetFilename)
    {
        AssetFilename = assetFilename;
    }

    [Export] public string AssetFilename { get; set; } = string.Empty;

    public void OpenSourceFile(SupportedGame game)
    {
        if (game == SupportedGame.Unknown) {
            GD.PrintErr("Unknown game for asset " + AssetFilename);
            return;
        }
        string file = Importer.ResolveSourceFilePath(AssetFilename, ReachForGodot.GetAssetConfig(game)).Replace('/', '\\');
        GD.Print(file);
        if (File.Exists(file)) {
            Process.Start(new ProcessStartInfo("explorer.exe") {
                UseShellExecute = false,
                Arguments = $"/select, \"{file}\"",
            });
        }
    }
}