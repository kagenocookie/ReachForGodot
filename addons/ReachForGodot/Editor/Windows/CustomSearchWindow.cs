namespace ReaGE;

using System;
using Godot;

[GlobalClass, Tool]
public partial class CustomSearchWindow : Window
{
    public string SearchedQuery { get; private set; } = string.Empty;
    private SearchType _type;
    public SearchType Type
    {
        get => _type;
        set {
            _type = value;
            if (filterTypeBtn != null) {
                filterTypeBtn.Selected = (int)value;
            }
        }
    }
    public Node? SearchRoot { get; set; }

    [Export] private LineEdit lineEdit = null!;
    [Export] private OptionButton filterTypeBtn = null!;
    [Export] private Container resultsContainer = null!;

    [Export] private PackedScene searchResultItemTemplate = null!;
    [Export] private int resultLimit = 100;

    private SearchFunction Filter = SearchNodesByName;

    private const string SceneFilepath = "res://addons/ReachForGodot/Editor/Windows/CustomSearchWindow.tscn";

    private readonly List<SearchResultItem> Results = new();

    public enum SearchType
    {
        NodeName,
        GameObjectName,
        Guid,
    }

    public enum SearchTargetType
    {
        Node,
        Resource,
    }

    private delegate bool SearchFunction(CustomSearchWindow self, Node node, out string? summary);

    public CustomSearchWindow()
    {
    }

    public CustomSearchWindow(Node? searchRoot)
    {
        SearchRoot = searchRoot;
    }

    public override void _Ready()
    {
        filterTypeBtn.Clear();
        foreach (var label in Enum.GetNames<SearchType>()) {
            filterTypeBtn.AddItem(label);
        }
        filterTypeBtn.Selected = (int)Type;
        filterTypeBtn.ItemSelected += OnFilterTypeChanged;
    }

    private void HandleCloseRequested()
    {
        QueueFree();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Keycode == Key.Escape) {
            HandleCloseRequested();
        }
    }

    public static void ShowNameSearch(Node source)
    {
        var wnd = ResourceLoader.Load<PackedScene>(SceneFilepath).Instantiate<CustomSearchWindow>();
        wnd.SearchRoot = source;
        EditorInterface.Singleton.PopupDialogCentered(wnd);
    }
    public static void ShowGameObjectSearch(Node source)
    {
        var wnd = ResourceLoader.Load<PackedScene>(SceneFilepath).Instantiate<CustomSearchWindow>();
        wnd.SearchRoot = source;
        wnd.Type = source is REGameObject or SceneFolder ? SearchType.GameObjectName : SearchType.NodeName;
        EditorInterface.Singleton.PopupDialogCentered(wnd);
    }

    public static void ShowGuidSearch(Guid guid, Node source)
    {
        var wnd = ResourceLoader.Load<PackedScene>(SceneFilepath).Instantiate<CustomSearchWindow>();
        wnd.SearchedQuery = guid.ToString();
        wnd.SearchRoot = source;
        EditorInterface.Singleton.PopupDialogCentered(wnd);
    }

    private void OnFilterTypeChanged(long id)
    {
        Type = (SearchType)id;
        Refilter();
    }

    private void ClearResults()
    {
        Results.Clear();
        resultsContainer.ClearChildren();
    }

    private void OnFilterUpdated(string text)
    {
        SearchedQuery = text;
        if (Type is not SearchType.Guid && Guid.TryParse(text, out var guid)) {
            Type = SearchType.Guid;
        }

        Refilter();
    }

    private void Refilter()
    {

        StartSearch();
    }

    private void OnVisibilityChanged()
    {
        if (Visible) {
            lineEdit.CallDeferred(Control.MethodName.GrabFocus);
            CallDeferred(MethodName.StartSearch);
        }
    }

    private void StartSearch()
    {
        if (!Visible) return;

        switch (Type) {
            case SearchType.Guid:
                Filter = SearchByGuid;
                break;
            case SearchType.GameObjectName:
                Filter = SearchGameObjectsByName;
                break;

            case SearchType.NodeName:
            default:
                Filter = SearchNodesByName;
                break;
        }

        var sceneRoot = EditorInterface.Singleton.GetEditedSceneRoot();
        SearchRoot ??= sceneRoot;
        if (SearchRoot == null) return;

        ClearResults();
        SearchRecurse(SearchRoot);
    }

    private void SearchRecurse(Node node)
    {
        if (Results.Count >= resultLimit) {
            return;
        }

        if (Filter.Invoke(this, node, out var summary)) {
            AddSearchResultItem(node, summary);
        }

        foreach (var child in node.GetChildren()) {
            SearchRecurse(child);
        }
    }

    private static bool SearchByGuid(CustomSearchWindow self, Node node, out string? summary)
    {
        if (node is REGameObject obj && obj.Uuid == self.SearchedQuery) {
            summary = obj.ToString();
            return true;
        }

        summary = null;
        return false;
    }

    private static bool SearchGameObjectsByName(CustomSearchWindow self, Node node, out string? summary)
    {
        if (node is REGameObject obj && obj.OriginalName.Contains(self.SearchedQuery!, StringComparison.OrdinalIgnoreCase)) {
            summary = node.Name;
            return true;
        }

        summary = null;
        return false;
    }

    private static bool SearchNodesByName(CustomSearchWindow self, Node node, out string? summary)
    {
        if (node.Name.ToString().Contains(self.SearchedQuery!, StringComparison.OrdinalIgnoreCase)) {
            summary = node.Name;
            return true;
        }

        summary = null;
        return false;
    }

    private void AddSearchResultItem(GodotObject target, string? summary)
    {
        var ui = searchResultItemTemplate.Instantiate<SearchResultItem>();
        Results.Add(ui);
        string? context = null;
        if (target is Node node) {
            summary ??= node.Name;
            ui.Pressed += () => {
                EditorInterface.Singleton.EditNode(node);
                HandleCloseRequested();
            };
            if (node is REGameObject obj) context = obj.Path;
            if (node is SceneFolder scn) context = scn.Path;
        } else if (target is Resource res) {
            summary ??= res.ResourceName;
            ui.Pressed += () => {
                EditorInterface.Singleton.EditResource(res);
                HandleCloseRequested();
            };
        }

        if (context == null && target is IAssetPointer ass) context = ass.Asset?.AssetFilename;

        ui.Setup(summary, context, target);
        resultsContainer.AddChild(ui);
    }
}