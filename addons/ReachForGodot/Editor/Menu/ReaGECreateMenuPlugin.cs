#if TOOLS
using System.Threading.Tasks;
using Godot;

namespace ReaGE;

public partial class ReaGECreateMenuPlugin : EditorContextMenuPlugin
{
    private Texture2D? _logo;
    private Texture2D Logo => _logo ??= ResourceLoader.Load<Texture2D>("res://addons/ReachForGodot/icons/logo.png");

    public override void _PopupMenu(string[] paths)
    {
        if (paths.Length != 1 || paths[0].StartsWith(ReachForGodot.GetUserdataBasePath(string.Empty))) return;
        var config = PathUtils.GuessAssetConfigFromImportPath(paths[0]);
        ShowTemplateOptions(paths[0], config?.Game ?? SupportedGame.Unknown);
    }

    private void ShowTemplateOptions(string targetPath, SupportedGame game)
    {
        if (game != SupportedGame.Unknown) {
            var popup = CreatePrefabPopup(targetPath, game);
            if (popup != null) {
                AddContextSubmenuItem("Prefab ...", popup);
            }
        } else {
            var popup = new PopupMenu();
            var totalCount = 0;
            foreach (var configured in ReachForGodot.ConfiguredGames) {
                var gamePopup = CreatePrefabPopup(targetPath, configured);
                if (gamePopup?.ItemCount > 0) {
                    popup.AddSubmenuNodeItem(configured.ToString().Capitalize(), gamePopup);
                    totalCount++;
                }
            }
            if (totalCount > 0) {
                AddContextSubmenuItem("Prefabs ...", popup, Logo);
            }
        }
    }

    private PopupMenu? CreatePrefabPopup(string targetPath, SupportedGame game)
    {
        var templates = ObjectTemplateManager.GetAvailableTemplates(ObjectTemplateType.GameObject, game);
        if (templates.Length == 0) return null;

        var menu = new PopupMenu();
        int i = 0;
        foreach (var template in templates) {
            menu.AddItem(Path.GetFileNameWithoutExtension(template).Capitalize(), i++);
        }
        menu.IdPressed += (id) => _ = HandleTemplateItems(targetPath, id, game);
        return menu;
    }

    private async Task HandleTemplateItems(string targetPath, long id, SupportedGame game)
    {
        var templates = ObjectTemplateManager.GetAvailableTemplates(ObjectTemplateType.GameObject, game);
        var chosenTemplate = templates[id];

        var obj = ObjectTemplateManager.InstantiateGameobject(chosenTemplate, null, null);
        if (obj != null) {
            await obj.ReSetupComponents();
            var baseOutputPath = Path.Combine(targetPath, obj.Name);
            var outputPath = baseOutputPath + ".pfb.tscn";
            int i = 0;
            while (ResourceLoader.Exists(outputPath)) {
                outputPath = baseOutputPath + "_" + (i++) + ".pfb.tscn";
            }

            obj.SaveAsScene(outputPath);
            await ResourceImportHandler.EnsureImported<PackedScene>(outputPath) ;
        }
    }
}
#endif
