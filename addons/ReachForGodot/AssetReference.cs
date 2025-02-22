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

    public string? ResolveSourceFile(AssetConfig config) => Importer.ResolveSourceFilePath(AssetFilename, config);
    public string? GetImportFilepath(AssetConfig config) => Importer.GetLocalizedImportPath(AssetFilename, config);

    public void OpenSourceFile(SupportedGame game)
    {
        if (game == SupportedGame.Unknown) {
            GD.PrintErr("Unknown game for asset " + AssetFilename);
            return;
        }
        var file = ResolveSourceFile(ReachForGodot.GetAssetConfig(game))?.Replace('/', '\\');
        if (File.Exists(file)) {
            GD.Print("Filename: " + file);
            Process.Start(new ProcessStartInfo("explorer.exe") {
                UseShellExecute = false,
                Arguments = $"/select, \"{file}\"",
            });
        } else {
            GD.PrintErr("File not found: " + file);
        }
    }
}