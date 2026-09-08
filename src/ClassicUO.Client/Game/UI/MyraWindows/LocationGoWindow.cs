using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Game.UI.MyraWindows.Widgets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;

namespace ClassicUO.Game.UI.MyraWindows;

/// <summary>Coordinate entry with per-character history, saved destinations and enabled map markers.</summary>
public sealed partial class LocationGoWindow : MyraControl
{
    [GeneratedRegex(@"^(?<X>\d+)\s*[,:\s]\s*(?<Y>\d+)$")]
    private static partial Regex PointCoordsRegex();

    private const int PageSize = 25;
    private readonly World _world;
    private readonly Profile _profile;
    private readonly WorldMapGump _mapGump;
    private readonly Map.Map _map;
    private readonly bool _pathfinding;
    private readonly Action<int, int> _goTo;
    private readonly Action _onClear;
    private readonly MyraInputBox _inputBox;
    private readonly MyraInputBox _descriptionBox;
    private readonly MyraInputBox _searchBox;
    private readonly MyraLabel _decodeLabel;
    private readonly MyraButton _goButton;
    private readonly MyraButton _saveButton;
    private readonly MyraTabControl _tabs;
    private Point _location;
    private int _page;

    public LocationGoWindow(World world, Action<int, int> goTo, Action onClear,
        Point? prefilledLocation = null, WorldMapGump mapGump = null, bool pathfinding = false)
        : base(TazLang.Get(pathfinding ? "map_pathfind_location" : "map_goto_title"))
    {
        _world = world;
        _profile = ProfileManager.CurrentProfile;
        _mapGump = mapGump;
        _pathfinding = pathfinding;
        _map = pathfinding ? world.Map : mapGump?.LocationMap ?? world.Map;
        _goTo = goTo;
        _onClear = onClear;
        _location = Sextant.InvalidPoint;

        var layout = new VerticalStackPanel { Spacing = 8, Padding = new Thickness(8), Width = 620 };
        var inputRow = new HorizontalStackPanel { Spacing = 4 };
        _inputBox = new MyraInputBox
        {
            Width = 300,
            HintText = TazLang.Get("map_goto_hint"),
            Text = prefilledLocation.HasValue ? $"{prefilledLocation.Value.X}, {prefilledLocation.Value.Y}" : ""
        };
        _inputBox.TextChangedByUser += (_, _) => UpdateDecode();
        _inputBox.KeyDown += (_, e) => { if (e.Data == Keys.Enter) Submit(); };
        inputRow.Widgets.Add(_inputBox);
        inputRow.Widgets.Add(new MyraButton(TazLang.Get("map_goto_clear"), Clear));
        _goButton = new MyraButton(ActionText, Submit);
        inputRow.Widgets.Add(_goButton);
        layout.Widgets.Add(inputRow);

        var descriptionRow = new HorizontalStackPanel { Spacing = 4 };
        _descriptionBox = new MyraInputBox { Width = 360, HintText = TazLang.Get("map_goto_description") };
        _descriptionBox.TextChangedByUser += (_, _) => UpdateDecode();
        _descriptionBox.KeyDown += (_, e) => { if (e.Data == Keys.Enter) Submit(); };
        descriptionRow.Widgets.Add(_descriptionBox);
        _saveButton = new MyraButton(TazLang.Get("map_goto_save"), () => Defer(SaveLocation))
            { Tooltip = TazLang.Get("map_goto_save_hint") };
        descriptionRow.Widgets.Add(_saveButton);
        layout.Widgets.Add(descriptionRow);

        _decodeLabel = new MyraLabel("", MyraLabel.TextStyle.P);
        layout.Widgets.Add(_decodeLabel);
        layout.Widgets.Add(new MyraLabel(TazLang.Get("map_goto_examples"), MyraLabel.TextStyle.P));

        var searchRow = new HorizontalStackPanel { Spacing = 4 };
        _searchBox = new MyraInputBox { Width = 360, HintText = TazLang.Get("map_goto_search") };
        _searchBox.TextChangedByUser += (_, _) => Defer(() => RefreshList(true));
        searchRow.Widgets.Add(_searchBox);
        searchRow.Widgets.Add(new MyraButton(TazLang.Get("map_goto_refresh"), () => Defer(() => RefreshList(true))));
        layout.Widgets.Add(searchRow);

        _tabs = new MyraTabControl();
        _tabs.AddTab(TazLang.Get("map_goto_recent"), () => BuildList(0));
        _tabs.AddTab(TazLang.Get("map_goto_saved"), () => BuildList(1));
        _tabs.AddTab(TazLang.Get("map_goto_filtered"), () => BuildList(2), TazLang.Get("map_goto_filtered_hint"));
        _tabs.SelectedIndexChanged += (_, _) => Defer(() => RefreshList(true));
        _tabs.SelectFirst();
        layout.Widgets.Add(_tabs);

        SetRootContent(layout);
        UpdateDecode();
        CenterInViewPort();
        UIManager.Add(this);
        BringOnTop();
        UIManager.KeyboardFocusControl = this;
        _inputBox.SetKeyboardFocus();
    }

    private string ActionText => TazLang.Get(_pathfinding ? "map_goto_pathfind" : "map_goto_button");

    /// <summary>Focus the current dialog, replacing it when the requested navigation mode changes.</summary>
    public static void Show(World world, Action<int, int> goTo, Action onClear,
        Point? prefilledLocation = null, WorldMapGump mapGump = null, bool pathfinding = false)
    {
        foreach (IGui gump in UIManager.Gumps)
            if (gump is LocationGoWindow window && !window.IsDisposed)
            {
                if (!window._disposeRequested && window.ContextIsCurrent && window._world == world
                    && window._mapGump == mapGump && window._pathfinding == pathfinding)
                {
                    window.BringOnTop();
                    window.SetKeyboardFocus();
                    window.Defer(() => window.RefreshList());
                    return;
                }
                window.Dispose();
            }

        _ = new LocationGoWindow(world, goTo, onClear, prefilledLocation, mapGump, pathfinding);
    }

    internal static bool ParsePoint(string text, out Point point)
    {
        point = Sextant.InvalidPoint;
        Match match = PointCoordsRegex().Match(text?.Trim() ?? "");
        if (!match.Success || !int.TryParse(match.Groups["X"].Value, out int x)
            || !int.TryParse(match.Groups["Y"].Value, out int y))
            return false;

        point = new Point(x, y);
        return true;
    }

    private void UpdateDecode()
    {
        string text = _inputBox.Text?.Trim() ?? "";
        _location = Sextant.InvalidPoint;
        bool valid = _map != null && text.Length > 0
            && (ParsePoint(text, out _location) || Sextant.Parse(_map, text, out _location))
            && IsInMap(_location);

        if (!valid)
            _location = Sextant.InvalidPoint;

        _decodeLabel.Text = text.Length == 0 ? "" : valid
            ? TazLang.Get("map_goto_decoded", [_location.X.ToString(), _location.Y.ToString()])
            : TazLang.Get("map_goto_invalid");
        _decodeLabel.TextColor = valid ? Color.LightGreen : Color.OrangeRed;
        _goButton.Enabled = valid || (_onClear != null && text.Length == 0);
        _saveButton.Enabled = valid && _profile != null && !string.IsNullOrWhiteSpace(_descriptionBox.Text);
    }

    private bool IsInMap(Point point) => point.X >= 0 && point.Y >= 0
        && point.X < Client.Game.UO.FileManager.Maps.MapsDefaultSize[_map.Index, 0]
        && point.Y < Client.Game.UO.FileManager.Maps.MapsDefaultSize[_map.Index, 1];

    private bool ContextIsCurrent => _map != null && ReferenceEquals(_profile, ProfileManager.CurrentProfile)
        && (_mapGump == null || !_mapGump.IsDisposed)
        && (_pathfinding ? _world.Map?.Index == _map.Index
            : (_mapGump?.LocationMap ?? _world.Map)?.Index == _map.Index);

    public override void Update()
    {
        // A facet/profile change must never reinterpret an old destination on the new map.
        if (!ContextIsCurrent)
            _disposeRequested = true;
        base.Update();
    }

    private IEnumerable<MapLocation> GetLocations(int tab)
    {
        IEnumerable<MapLocation> locations;
        if (tab == 0)
            locations = _profile?.WorldMapRecentLocations?.Where(l => l != null).Take(MapLocationHistory.RecentLimit) ?? [];
        else if (tab == 1)
            locations = (_profile?.WorldMapSavedLocations ?? [])
                .Concat(_mapGump?.GetCustomLocations() ?? []).Where(l => l != null)
                .DistinctBy(l => (l.MapId, l.X, l.Y));
        else
            locations = _mapGump?.GetFilteredLocations() ?? [];

        locations = locations.Where(l => MapLocationHistory.Matches(l, _searchBox.Text));
        return tab == 0 ? locations : locations.OrderBy(l => l.MapId != _map.Index)
            .ThenBy(l => l.Description, StringComparer.OrdinalIgnoreCase).ThenBy(l => l.X).ThenBy(l => l.Y);
    }

    private Widget BuildList(int tab)
    {
        List<MapLocation> locations = GetLocations(tab).ToList();
        int pages = Math.Max(1, (locations.Count + PageSize - 1) / PageSize);
        _page = Math.Clamp(_page, 0, pages - 1);
        var body = new VerticalStackPanel { Spacing = 4 };
        var rows = new VerticalStackPanel { Spacing = 4 };

        foreach (MapLocation location in locations.Skip(_page * PageSize).Take(PageSize))
        {
            var row = new HorizontalStackPanel { Spacing = 4 };
            string description = string.IsNullOrWhiteSpace(location.Description)
                ? TazLang.Get("map_goto_unnamed") : location.Description;
            string coordinates = TazLang.Get("map_goto_coordinates",
                [location.X.ToString(), location.Y.ToString(), location.MapId.ToString()]);
            var label = new VerticalStackPanel { Width = 285 };
            label.Widgets.Add(new MyraLabel(description, MyraLabel.TextStyle.P) { Width = 285, Tooltip = description });
            label.Widgets.Add(new MyraLabel(coordinates, MyraLabel.TextStyle.P));
            row.Widgets.Add(label);
            bool usable = location.MapId == _map.Index && IsInMap(new Point(location.X, location.Y));
            string tooltip = TazLang.Get(usable ? "map_goto_use_hint"
                : location.MapId != _map.Index ? "map_goto_other_map" : "map_goto_invalid");
            row.Widgets.Add(new MyraButton(TazLang.Get("map_goto_use"), () => SelectLocation(location))
                { Enabled = usable, Tooltip = tooltip });
            row.Widgets.Add(new MyraButton(ActionText, () => { SelectLocation(location); Submit(); })
                { Enabled = usable, Tooltip = usable ? coordinates : tooltip });

            if (tab == 1 && (_profile?.WorldMapSavedLocations?.Any(location.SamePosition) ?? false))
                row.Widgets.Add(new MyraButton(TazLang.Get("remove"), () => Defer(() => RemoveSaved(location))));
            rows.Widgets.Add(row);
        }

        if (locations.Count == 0)
            rows.Widgets.Add(new MyraLabel(TazLang.Get(tab switch
            {
                0 => "map_goto_no_recent", 1 => "map_goto_no_saved", _ => "map_goto_no_filtered"
            }), MyraLabel.TextStyle.P) { Width = 550 });

        body.Widgets.Add(new ScrollViewer { Height = 235, Content = rows });
        var paging = new HorizontalStackPanel { Spacing = 8 };
        paging.Widgets.Add(new MyraButton("<", () => Defer(() => { _page--; RefreshList(); })) { Enabled = _page > 0 });
        paging.Widgets.Add(new MyraLabel($"{_page + 1} / {pages} ({locations.Count})", MyraLabel.TextStyle.P));
        paging.Widgets.Add(new MyraButton(">", () => Defer(() => { _page++; RefreshList(); })) { Enabled = _page + 1 < pages });
        body.Widgets.Add(paging);
        return body;
    }

    private void RefreshList(bool resetPage = false)
    {
        if (resetPage) _page = 0;
        _tabs.RefreshSelectedContent();
    }

    private void SelectLocation(MapLocation location)
    {
        if (!ContextIsCurrent || location.MapId != _map.Index)
            return;
        _inputBox.Text = $"{location.X}, {location.Y}";
        _descriptionBox.Text = location.Description ?? "";
        UpdateDecode();
        _descriptionBox.SetKeyboardFocus();
    }

    private MapLocation CurrentLocation() => new()
    {
        MapId = _map.Index, X = _location.X, Y = _location.Y, Description = _descriptionBox.Text?.Trim() ?? ""
    };

    private void SaveLocation()
    {
        UpdateDecode();
        if (!ContextIsCurrent || !_saveButton.Enabled)
            return;

        MapLocation location = CurrentLocation();
        _profile.WorldMapSavedLocations = MapLocationHistory.Record(_profile.WorldMapSavedLocations, location, int.MaxValue);
        // Renaming a recent entry should not change its position in the history.
        _profile.WorldMapRecentLocations = (_profile.WorldMapRecentLocations ?? []).Where(l => l != null)
            .Select(l => location.SamePosition(l) ? location : l).ToList();
        _profile.Save();
        _searchBox.Text = "";
        _tabs.SelectedIndex = 1;
        RefreshList(true);
    }

    private void RemoveSaved(MapLocation location)
    {
        if (!ContextIsCurrent || _profile == null) return;
        _profile.WorldMapSavedLocations = (_profile.WorldMapSavedLocations ?? []).Where(l => !location.SamePosition(l)).ToList();
        _profile.Save();
        RefreshList();
    }

    private void Clear()
    {
        _inputBox.Text = "";
        _descriptionBox.Text = "";
        UpdateDecode();
        _inputBox.SetKeyboardFocus();
    }

    private void Submit()
    {
        if (!ContextIsCurrent || _disposeRequested)
            return;
        UpdateDecode();
        if (_onClear != null && string.IsNullOrWhiteSpace(_inputBox.Text))
        {
            _onClear();
            _disposeRequested = true;
            return;
        }
        if (_location == Sextant.InvalidPoint)
            return;

        if (_profile != null)
        {
            _profile.WorldMapRecentLocations = MapLocationHistory.Record(_profile.WorldMapRecentLocations, CurrentLocation());
            _profile.Save();
        }
        _goTo(_location.X, _location.Y);
        _disposeRequested = true;
    }
}
