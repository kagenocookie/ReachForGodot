#if TOOLS
using Godot;
using Godot.Collections;

namespace ReaGE;

public partial class GameResourceImporter : EditorImportPlugin
{
    public override string[] _GetRecognizedExtensions()
    {
        return PathUtils.GetKnownImportableFileVersions();
    }

    public override string _GetImporterName() => "Reach for Godot";
    public override string _GetVisibleName() => "RE Engine resource";
    public override string _GetResourceType() => "Resource";
    public override string _GetSaveExtension() => "tres";
    public override int _GetFormatVersion() => 1;
    public override float _GetPriority() => 1f;
    public override int _GetImportOrder() => 0;
    public override int _GetPresetCount() => 0;
    public override bool _GetOptionVisibility(string path, StringName optionName, Dictionary options) => true;

    public override Array<Dictionary> _GetImportOptions(string path, int presetIndex)
    {
        var configuredGames = ReachForGodot.ConfiguredGames;
        var format = PathUtils.GetFileFormat(path);
        if (format.version != -1 && format.format != SupportedFileFormats.Unknown) {
            var ext = PathUtils.GetFilepathWithoutVersion(path).GetExtension();
            configuredGames = configuredGames.Where(game =>
                PathUtils.GuessFileVersion(ext, format.format, ReachForGodot.GetAssetConfig(game)) == format.version);
        }
        var count = configuredGames.Count();

        return [
            new Godot.Collections.Dictionary
            {
                { "name", "game" },
                { "default_value", count == 0 ? -1 : 0 },
                { "property_hint", (int)PropertyHint.Enum },
                { "hint_string", count switch {
                    0 => "Unsupported",
                    1 => configuredGames.First().ToString(),
                    _ => "Auto-detect:0," + string.Join(",", configuredGames.Select((g) => g + ":" + (int)g ))
                } },
            },
        ];
    }

    public override Error _Import(string sourceFile, string savePath, Dictionary options, Array<string> platformVariants, Array<string> genFiles)
    {
        SupportedGame game = (SupportedGame)options["game"].AsInt32();
        if ((int)game == -1) {
            return Error.FileUnrecognized;
        }
        var config = game == SupportedGame.Unknown ? PathUtils.GuessAssetConfigFromImportPath(sourceFile) : ReachForGodot.GetAssetConfig(game);
        if (config == null) {
            if (game == SupportedGame.Unknown) {
                GD.PrintErr($"Could not determine game for file {sourceFile}.\nImportable files should be placed within an AssetConfig-specified folder or manually setting the game from the import settings.");
            } else {
                GD.PrintErr($"Invalid or unconfigured game for import.");
            }
            return Error.CantResolve;
        }

        var format = PathUtils.GetFileFormat(sourceFile);
        if (format.version == -1) return Error.CantResolve;

        var importedSavePath = $"{savePath}.{_GetSaveExtension()}";
        var globalSource = ProjectSettings.GlobalizePath(sourceFile);
        var godotExt = format.format is SupportedFileFormats.Scene or SupportedFileFormats.Prefab ? "tscn" : "tres";
        var resourceImportPath =  $"{sourceFile}.{godotExt}";
        var altResourcePath =  $"{PathUtils.GetFilepathWithoutVersion(sourceFile)}.{godotExt}";
        var relative = PathUtils.ImportPathToRelativePath(sourceFile, config)
            ?? sourceFile;

        var resource = ResourceLoader.Exists(resourceImportPath) ? ResourceLoader.Load<Resource>(resourceImportPath)
            : ResourceLoader.Exists(altResourcePath) ? ResourceLoader.Load<Resource>(altResourcePath)
            : null;
        if (resource != null) {
            var isOk = false;
            if (resource is REResource existingResource) {
                isOk = existingResource.Game == config.Game && (existingResource as IImportableAsset)?.IsEmpty == false;
            } else if (resource is PackedScene pack) {
                // should we really instantiate for this, or just trust that it's correct?
                isOk = pack.Instantiate<IAssetPointer>()?.Game == config.Game;
            }
            if (isOk) {
                var replace = new ImportedResource() {
                    FileFormat = format.format,
                    Resource = resource,
                    Game = config.Game,
                    Asset = new AssetReference(relative),
                };
                ResourceSaver.Save(replace, importedSavePath);
                return Error.Ok;
            }
        }

        config.Paths.SourcePathOverride = config.AssetDirectory;
        resource = Importer.Import(globalSource, config, resourceImportPath, false)!;
        if (resource == null) {
            config.Paths.SourcePathOverride = null;
            return Error.FileNotFound;
        }
        if (resource is REResource engineResource) {
            engineResource.Asset ??= new();
            engineResource.Asset.AssetFilename = sourceFile;
        } else if (resource is PackedScene packed) {
            var root = packed.Instantiate<IAssetPointer>();
            root.Asset ??= new();
            root.Asset.AssetFilename = sourceFile;
            packed.Pack((Node)root);
        }

        if (ResourceLoader.Exists(resourceImportPath)) {
            resource.TakeOverPath(resourceImportPath);
        } else {
            resource.ResourcePath = resourceImportPath;
        }
        ResourceSaver.Save(resource, resourceImportPath);

        var imported = new ImportedResource() {
            FileFormat = format.format,
            Resource = resource,
            Game = config.Game,
            Asset = new AssetReference(relative),
        };
        ResourceSaver.Save(imported, importedSavePath);

        if (ShouldImportContent(format.format))  {
            var converter = new AssetConverter(config.Game, GodotImportOptions.directImport);
            var importable = (resource as PackedScene)?.Instantiate<IImportableAsset>() ?? resource as IImportableAsset;
            if (importable != null) {
                _ = converter.ImportAsset(importable, globalSource).ContinueWith(t => {
                    if (t.IsCompletedSuccessfully) {
                        if (resource is PackedScene packed) {
                            packed.Pack((Node)importable);
                        }
                        ResourceSaver.Save(resource);
                    }
                });
            }
        }
        config.Paths.SourcePathOverride = null;

        return Error.Ok;
    }

    private static bool ShouldImportContent(SupportedFileFormats format) => format is not SupportedFileFormats.Texture;
}
#endif