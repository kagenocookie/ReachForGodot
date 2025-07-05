using Godot;
using ReeLib;

namespace ReaGE;

[GlobalClass, Tool]
public partial class MainWindow : Control
{
    [Export] private EmbeddedFileUnpackerUI? fileBrowser;

    [Export] private Label? debugPanelNoteLabel;
    [Export] private OptionButton? debugGamePicker;

    public override void _EnterTree()
    {
        if (debugGamePicker != null) {
            debugGamePicker.Clear();
            debugGamePicker.AddItem("All", (int)SupportedGame.Unknown);
            foreach (var game in ReachForGodot.GameList) {
                if (ReachForGodot.GetAssetConfig(game).IsValid) {
                    debugGamePicker.AddItem(game.ToString(), (int)game);
                }
            }
        }
    }

    private void HandleSelectedFiles(string[] files)
    {
        var config = ReachForGodot.GetAssetConfig(fileBrowser!.Game);
        AssetBrowser.TriggerFileAction(files, config, fileBrowser!.ShouldImportFiles, fileBrowser.FileSystem!);
    }

    private void ResetResourceCache()
    {
        var selectedGame = (SupportedGame?)debugGamePicker?.GetSelectedId() ?? SupportedGame.Unknown;
        if (selectedGame == SupportedGame.Unknown) {
            ResourceRepository.ResetCache();
        } else {
            ResourceRepository.ResetCache(selectedGame.ToShortName());
        }
        if (debugPanelNoteLabel != null) {
            debugPanelNoteLabel.Visible = true;
        }
    }
}
