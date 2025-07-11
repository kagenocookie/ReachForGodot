#nullable enable

using System.Diagnostics.CodeAnalysis;
using Godot;

namespace ReaGE;

[GlobalClass, Tool]
public partial class AssetReference : Resource
{
    public AssetReference()
    {
    }

    public AssetReference(string assetFilename)
    {
        // normalize all paths to front slashes - because resources tend to use forward slashes
        AssetFilename = PathUtils.NormalizeResourceFilepath(assetFilename);
    }

    private string _assetFilename = string.Empty;
    [Export] public string AssetFilename
    {
        get => _assetFilename;
        set {_assetFilename = PathUtils.NormalizeResourceFilepath(value); EmitChanged();}
    }
    public string ExportedFilename
        => _assetFilename.StartsWith("res://")
            ? PathUtils.ImportPathToRelativePath(_assetFilename) ?? _assetFilename
            : _assetFilename;

    public ReadOnlySpan<char> BaseFilepath => PathUtils.GetFilepathWithoutExtensionOrVersion(_assetFilename);
    public ReadOnlySpan<char> BaseFilename => Path.GetFileName(PathUtils.GetFilepathWithoutExtensionOrVersion(_assetFilename));

    public bool IsEmpty => string.IsNullOrWhiteSpace(_assetFilename);

    public AssetReference Clone() => new AssetReference(_assetFilename);

    public bool IsSameAsset(string compare)
    {
        if (AssetFilename == compare) return true;

        var dot = AssetFilename.LastIndexOf('.');
        return dot == -1 ? false : AssetFilename.AsSpan().Slice(0, dot).SequenceEqual(compare);
    }

    public bool IsImportableAsset(AssetConfig config, [MaybeNullWhen(false)] out string assetImportPath)
    {
        assetImportPath = PathUtils.GetAssetImportPath(_assetFilename, config);
        return assetImportPath != null && FindSourceFile(config) != null;
    }

    public string? FindSourceFile(AssetConfig config) => PathUtils.FindSourceFilePath(AssetFilename, config);
    public string? GetImportFilepath(AssetConfig config) => PathUtils.GetLocalizedImportPath(AssetFilename, config);
    public string GetFilenameWithExtensions(AssetConfig config) => PathUtils.AppendFileVersion(Path.GetFileName(_assetFilename), config);
    public string? GetImportFilepathChangeExtension(AssetConfig config, string ext)
    {
        var path = PathUtils.GetLocalizedImportPath(AssetFilename, config);
        path = Path.ChangeExtension(path, ext);
        return path;
    }

    public void OpenSourceFile(SupportedGame game)
    {
        if (AssetFilename.StartsWith("res://")) {
            FileSystemUtils.ShowFileInExplorer(ProjectSettings.GlobalizePath(AssetFilename));
            return;
        }

        if (game == SupportedGame.Unknown) {
            GD.PrintErr("Unknown game for asset " + AssetFilename);
            return;
        }
        var file = PathUtils.FindSourceFilePath(AssetFilename, ReachForGodot.GetAssetConfig(game))?.Replace('/', '\\');
        FileSystemUtils.ShowFileInExplorer(file);
    }

    [return: NotNullIfNotNull(nameof(assref))]
    public static implicit operator string?(AssetReference? assref) => assref?.AssetFilename;

    public override string ToString() => AssetFilename;
}