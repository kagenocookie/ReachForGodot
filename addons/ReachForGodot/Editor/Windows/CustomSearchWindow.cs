namespace ReaGE;

using System;
using System.Threading.Tasks;
using Godot;
using ReaGE.Tools;
using RszTool.Efx;

[GlobalClass, Tool]
public partial class CustomSearchWindow : Window
{
    public string SearchedQuery { get; private set; } = string.Empty;
    private NodeSearchType _typeNode;
    public NodeSearchType SearchTypeNode
    {
        get => _typeNode;
        set {
            _typeNode = value;
            if (filterTypeBtn != null) {
                filterTypeBtn.Selected = (int)value;
            }
        }
    }

    private ExternalFileSearchType _typeExternal;
    public ExternalFileSearchType SearchTypeExternalFile
    {
        get => _typeExternal;
        set {
            _typeExternal = value;
            if (filterTypeBtn != null) {
                filterTypeBtn.Selected = (int)value;
            }
        }
    }

    private static CancellationTokenSource tokenSource = new();

    public SearchTargetType TargetType { get; set; }

    public Node? SearchRoot { get; set; }

    [Export] private LineEdit lineEdit = null!;
    [Export] private OptionButton searchTargetBtn = null!;
    [Export] private OptionButton filterTypeBtn = null!;
    [Export] private Container resultsContainer = null!;

    [Export] private PackedScene searchResultItemTemplate = null!;
    [Export] private int resultLimit = 100;

    private SearchFunction Filter = SearchNodesByName;

    private const string SceneFilepath = "res://addons/ReachForGodot/Editor/Windows/CustomSearchWindow.tscn";

    private readonly List<SearchResultItem> Results = new();

    public enum NodeSearchType
    {
        NodeName,
        GameObjectName,
        Guid,
        ComponentClassname,
    }

    public enum ExternalFileSearchType
    {
        ComponentInRSZFiles,
        EfxAttributeType,
        RcolActionName,
    }

    public enum SearchTargetType
    {
        Node,
        ExternalFile,
        // Resource,
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
        searchTargetBtn.Clear();
        foreach (var label in Enum.GetNames<SearchTargetType>()) {
            searchTargetBtn.AddItem(label.Capitalize());
        }
        searchTargetBtn.Selected = (int)TargetType;
        searchTargetBtn.ItemSelected += OnTargetTypeChanged;
        filterTypeBtn.ItemSelected += OnFilterTypeChanged;
        RefreshFilterTypeOptions();
    }

    private void RefreshFilterTypeOptions()
    {
        filterTypeBtn.ItemSelected -= OnFilterTypeChanged;
        filterTypeBtn.Clear();
        var names = TargetType switch {
            SearchTargetType.Node => Enum.GetNames<NodeSearchType>(),
            SearchTargetType.ExternalFile => Enum.GetNames<ExternalFileSearchType>(),
            _ => Array.Empty<string>(),
        };
        foreach (var label in names) {
            filterTypeBtn.AddItem(label.Capitalize());
        }
        filterTypeBtn.Selected = (int)SearchTypeNode;
        filterTypeBtn.ItemSelected += OnFilterTypeChanged;
    }

    private void HandleCloseRequested()
    {
        tokenSource.Cancel();
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
        wnd.TargetType = SearchTargetType.Node;
        wnd.SearchRoot = source;
        EditorInterface.Singleton.PopupDialogCentered(wnd);
    }
    public static void ShowGameObjectSearch(Node source)
    {
        var wnd = ResourceLoader.Load<PackedScene>(SceneFilepath).Instantiate<CustomSearchWindow>();
        wnd.TargetType = SearchTargetType.Node;
        wnd.SearchRoot = source;
        wnd.SearchTypeNode = source is GameObject or SceneFolder ? NodeSearchType.GameObjectName : NodeSearchType.NodeName;
        EditorInterface.Singleton.PopupDialogCentered(wnd);
    }

    public static void ShowGuidSearch(Guid guid, Node source)
    {
        var wnd = ResourceLoader.Load<PackedScene>(SceneFilepath).Instantiate<CustomSearchWindow>();
        wnd.TargetType = SearchTargetType.Node;
        wnd.SearchedQuery = guid.ToString();
        wnd.SearchRoot = source;
        EditorInterface.Singleton.PopupDialogCentered(wnd);
    }

    public static void ShowFileSearch(ExternalFileSearchType type, string? initialQuery)
    {
        var wnd = ResourceLoader.Load<PackedScene>(SceneFilepath).Instantiate<CustomSearchWindow>();
        wnd.TargetType = SearchTargetType.ExternalFile;
        wnd.SearchedQuery = initialQuery ?? string.Empty;

        EditorInterface.Singleton.PopupDialogCentered(wnd);
    }

    private void OnTargetTypeChanged(long id)
    {
        TargetType = (SearchTargetType)id;
        RefreshFilterTypeOptions();
        Refilter();
    }

    private void OnFilterTypeChanged(long id)
    {
        switch (TargetType) {
            case SearchTargetType.Node:
                SearchTypeNode = (NodeSearchType)id;
                break;
            // case SearchTargetType.Resource:
                // SearchTypeExternalFile = (ExternalFileSearchType)id;
                // break;
            case SearchTargetType.ExternalFile:
                SearchTypeExternalFile = (ExternalFileSearchType)id;
                break;
        }
        Refilter();
    }

    private void ClearResults()
    {
        Results.Clear();
        resultsContainer.QueueFreeChildren();
    }

    private void OnFilterUpdated(string text)
    {
        SearchedQuery = text;
        if (SearchTypeNode is not NodeSearchType.Guid && Guid.TryParse(text, out var guid)) {
            SearchTypeNode = NodeSearchType.Guid;
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
        } else {
            tokenSource.Cancel();
        }
    }

    private void StartSearch()
    {
        if (!tokenSource.IsCancellationRequested) tokenSource.Cancel();
        if (!Visible) return;

        tokenSource = new();

        switch (TargetType) {
            case SearchTargetType.Node:
                StartNodeSearch();
                break;
            case SearchTargetType.ExternalFile:
                StartExternalFileSearch();
                break;
        }
    }

    private void StartExternalFileSearch()
    {
        if (!Visible) return;

        ClearResults();
        if (string.IsNullOrEmpty(SearchedQuery)) {
            return;
        }

        switch (SearchTypeExternalFile) {
            case ExternalFileSearchType.ComponentInRSZFiles:
                Task.Run(() => SearchFilesRSZInstances(SearchedQuery));
                break;
            case ExternalFileSearchType.EfxAttributeType:
                Task.Run(() => SearchFilesEFX(SearchedQuery));
                break;
            case ExternalFileSearchType.RcolActionName:
                Task.Run(() => SearchRcolName(SearchedQuery));
                break;
        }
    }

    private void StartNodeSearch()
    {
        if (!Visible) return;

        switch (SearchTypeNode) {
            case NodeSearchType.Guid:
                Filter = SearchByGuid;
                break;
            case NodeSearchType.GameObjectName:
                Filter = SearchGameObjectsByName;
                break;

            case NodeSearchType.ComponentClassname:
                Filter = SearchGameObjectsByComponent;
                break;

            case NodeSearchType.NodeName:
            default:
                Filter = SearchNodesByName;
                break;
        }

        var sceneRoot = EditorInterface.Singleton.GetEditedSceneRoot();
        SearchRoot ??= sceneRoot;
        if (SearchRoot == null) return;

        ClearResults();
        SearchNodes(SearchRoot);
    }

    private void SearchNodes(Node node)
    {
        if (Results.Count >= resultLimit) {
            return;
        }

        if (Filter.Invoke(this, node, out var summary)) {
            var ui = CreateSearchResultItem(summary, node);
            resultsContainer.AddChild(ui);
        }

        foreach (var child in node.GetChildren()) {
            SearchNodes(child);
        }
    }

    private void SearchFilesRSZInstances(string classname)
    {
        tokenSource.Cancel();
        tokenSource = new();

        foreach (var filepath in ReaGETools.FindInstancesInAnyRSZFile(classname, Array.Empty<SupportedGame>(), tokenSource.Token)) {
            if (Results.Count >= resultLimit) {
                return;
            }

            var ui = CreateSearchResultItem(filepath, null);
            ui.Pressed += () => {
                FileSystemUtils.ShowFileInExplorer(filepath);
            };
            resultsContainer.CallDeferred(Node.MethodName.AddChild, ui);
        }
    }

    private void SearchRcolName(string name)
    {
        tokenSource.Cancel();
        tokenSource = new();

        var conv = new AssetConverter(GodotImportOptions.testImport);
        foreach (var (game, filepath) in ReaGETools.FindFilesByFormat(SupportedFileFormats.Rcol, null, tokenSource.Token)) {
            conv.Game = game;

            using var f = conv.Rcol.CreateFile(new RszTool.FileHandler(filepath));
            f.Read();
            foreach (var set in f.RequestSets) {
                if (Results.Count >= resultLimit) {
                    return;
                }

                if (set.Info.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase)) {
                    var ui = CreateSearchResultItem(filepath, null);
                    ui.Pressed += () => {
                        FileSystemUtils.ShowFileInExplorer(filepath);
                    };
                    resultsContainer.CallDeferred(Node.MethodName.AddChild, ui);
                }
            }
        }
    }
    private void SearchFilesEFX(string attributeType)
    {
        tokenSource.Cancel();
        tokenSource = new();

        var words = attributeType.Split(' ');

        var efxTypeStr = words.FirstOrDefault(w => Enum.TryParse<EfxAttributeType>(w, out _));
        var efxVersionStr = words.FirstOrDefault(w => Enum.TryParse<EfxVersion>(w, out _));

        if (efxTypeStr == null || !Enum.TryParse<EfxAttributeType>(efxTypeStr, out var efxType)) {
            return;
        }
        var efxVersion = efxVersionStr != null && Enum.TryParse<EfxVersion>(efxVersionStr, out var efxV) ? efxV : EfxVersion.Unknown;

        foreach (var filepath in ReaGETools.FindEfxByAttribute(efxType, efxVersion, tokenSource.Token)) {
            if (Results.Count >= resultLimit) {
                return;
            }

            var ui = CreateSearchResultItem(filepath, null);
            ui.Pressed += () => {
                FileSystemUtils.ShowFileInExplorer(filepath);
            };
            resultsContainer.CallDeferred(Node.MethodName.AddChild, ui);
        }
    }

    private static bool SearchByGuid(CustomSearchWindow self, Node node, out string? summary)
    {
        if (node is GameObject obj && obj.Uuid == self.SearchedQuery) {
            summary = obj.ToString();
            return true;
        }

        summary = null;
        return false;
    }

    private static bool SearchGameObjectsByName(CustomSearchWindow self, Node node, out string? summary)
    {
        if (node is GameObject obj && obj.OriginalName.Contains(self.SearchedQuery!, StringComparison.OrdinalIgnoreCase)) {
            summary = obj.ToString();
            return true;
        }

        summary = null;
        return false;
    }

    private static bool SearchGameObjectsByComponent(CustomSearchWindow self, Node node, out string? summary)
    {
        if (node is GameObject obj && obj.Components.FirstOrDefault(c => true == c?.Classname?.Contains(self.SearchedQuery!, StringComparison.OrdinalIgnoreCase)) is REComponent comp) {
            summary = $"{node.Name} ({comp.Classname})";
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

    private SearchResultItem CreateSearchResultItem(string? summary, GodotObject? target)
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
            if (node is GameObject obj) context = obj.Path;
            if (node is SceneFolder scn) context = scn.Path;
        } else if (target is Resource res) {
            summary ??= res.ResourceName;
            ui.Pressed += () => {
                EditorInterface.Singleton.EditResource(res);
                HandleCloseRequested();
            };
        }

        if (context == null && target is IAssetPointer ass) context = ass.Asset?.AssetFilename;

        ui.Setup(summary, context, null);
        return ui;
    }
}