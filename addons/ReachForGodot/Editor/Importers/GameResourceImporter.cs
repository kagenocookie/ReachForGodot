#if TOOLS
using Godot;
using Godot.Collections;
using ReeLib;

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
        var configs = ReachForGodot.ConfiguredAssetConfigs;
        var format = PathUtils.ParseFileFormat(path);
        if (format.version != -1 && format.format != KnownFileFormats.Unknown) {
            var ext = PathUtils.GetFilepathWithoutVersion(path).GetExtension();
            configs = configs.Where(config => config.Workspace.GetFileVersion(ext) == format.version);
        }
        var count = configs.Count();

        return [
            new Godot.Collections.Dictionary
            {
                { "name", "game" },
                { "default_value", count == 0 ? -1 : 0 },
                { "property_hint", (int)PropertyHint.Enum },
                { "hint_string", count switch {
                    0 => "Unsupported",
                    1 => configs.First().ToString(),
                    _ => "Auto-detect:0," + string.Join(",", configs.Select((g) => g + ":" + (int)g.Game ))
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

        var format = PathUtils.ParseFileFormat(sourceFile);
        if (format.version == -1) return Error.CantResolve;

        var importedSavePath = $"{savePath}.{_GetSaveExtension()}";
        var globalSource = ProjectSettings.GlobalizePath(sourceFile);
        var resourceImportPath = $"{PathUtils.GetFilepathWithoutVersion(sourceFile)}.tres";
        var altResourcePath = $"{sourceFile}.tres";
        var relative = PathUtils.ImportPathToRelativePath(sourceFile, config)
            ?? sourceFile;

        var resource = ResourceLoader.Exists(resourceImportPath) ? ResourceLoader.Load<REResource>(resourceImportPath)
            : ResourceLoader.Exists(altResourcePath) ? ResourceLoader.Load<REResource>(altResourcePath)
            : null;
        if (resource != null) {
            var isOk = false;
            if (resource is REResourceProxy proxy && resource is not MeshResource && proxy.ImportedResource is PackedScene pack) {
                // should we really instantiate for this, or just trust that it's correct?
                isOk = pack.Instantiate<IAssetPointer>()?.Game == config.Game;
            } else {
                isOk = resource.Game == config.Game && (resource as IImportableAsset)?.IsEmpty == false;
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
        resource = Importer.ImportResource(globalSource, config, resourceImportPath, false)!;
        if (resource == null) {
            config.Paths.SourcePathOverride = null;
            return Error.FileNotFound;
        }
        if (resource is REResourceProxy proxy1 && proxy1.ImportedResource is PackedScene packed) {
            var root = packed.Instantiate<IAssetPointer>();
            root.Asset ??= new();
            root.Asset.AssetFilename = sourceFile;
            packed.Pack((Node)root);
        } else {
            resource.Asset ??= new();
            resource.Asset.AssetFilename = sourceFile;
        }

        resource.SaveOrReplaceResource(resourceImportPath);

        var imported = new ImportedResource() {
            FileFormat = format.format,
            Resource = resource,
            Game = config.Game,
            Asset = new AssetReference(relative),
        };
        ResourceSaver.Save(imported, importedSavePath);

        var converter = new AssetConverter(config.Game, GodotImportOptions.directImport);

        var asset = (resource as REResourceProxy)?.ImportedResource as PackedScene ?? resource as Resource;
        var importable = (asset as PackedScene)?.Instantiate<IImportableAsset>() ?? resource as IImportableAsset;
        if (importable != null) {
            _ = converter.ImportAsset(importable, globalSource).ContinueWith(t => {
                if (t.IsCompletedSuccessfully) {
                    ResourceSaver.Save(asset);
                }
            });
        }
        config.Paths.SourcePathOverride = null;

        return Error.Ok;
    }
}
#endif