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
        // normalize all paths to front slashes - because resources tend to use forward slashes
        AssetFilename = assetFilename.Replace('\\', '/');
    }

    [Export] public string AssetFilename { get; set; } = string.Empty;

    public bool IsSameAsset(string compare)
    {
        if (AssetFilename == compare) return true;

        var dot = AssetFilename.LastIndexOf('.');
        return dot == -1 ? false : AssetFilename.AsSpan().Slice(0, dot).SequenceEqual(compare);
    }

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