using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ClassicUO.Common;
using ClassicUO.Configuration;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Controls.ResizableComponents;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Game.UI.Gumps.GridHighLight;
using ClassicUO.Game.UI.MyraWindows;
using ClassicUO.Game.UI.MyraWindows.Widgets;
using ClassicUO.Input;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using SDL3;

namespace ClassicUO.Game.UI;

public sealed class NearbyLootGump : MyraControl
{
    private const int DefaultWidth = 300;
    private const int DefaultHeight = 550;
    private const int MinimumWidth = 250;
    private const int MinimumHeight = 200;
    private const int ItemIconSize = 36;
    private const long CorpseCacheLifetime = 120_000;

    private static readonly HashSet<uint> _corpsesRequested = [];
    private static readonly HashSet<uint> _openedCorpses = [];

    private readonly World _world;
    private readonly HashSet<LootGroupKey> _expandedGroups = [];
    private readonly List<NavigationEntry> _navigationEntries = [];
    private readonly List<NearbyLootIconFrame> _iconFrames = [];
    private readonly List<NearbyLootRow> _lootRows = [];
    private readonly List<Item> _visibleLootItems = [];
    private readonly VerticalStackPanel _lootList = new()
    {
        Spacing = 2,
        Padding = new Thickness(2)
    };

    private MyraButton _lootButton;
    private bool _refreshRequested;
    private bool _eventsSubscribed;
    private int _selectedIndex = -1;
    private long _nextClean;

    public NearbyLootGump(World world)
        : base(TazLang.Get("nearbyloot_title", "Nearby corpse loot"))
    {
        UIManager.GetGump<NearbyLootGump>()?.Dispose();

        _world = world ?? throw new ArgumentNullException(nameof(world));
        CanBeSaved = true;
        AcceptKeyboardInput = true;
        AcceptMouseInput = true;

        ConfigureWindow();
        BuildWindow();
        CenterInViewPort();
        SubscribeEvents();
        RequestUpdateContents();
    }

    private void ConfigureWindow()
    {
        _rootWindow.Props.Resize.MinWidth = MinimumWidth;
        _rootWindow.Props.Resize.MinHeight = MinimumHeight;
        _rootWindow.Props.Resize.ScrollerMode = ScrollViewerMode.None;
        _rootWindow.Props.InitialSizeStore = new Accessor<Point?>(GetStoredSize, StoreSize);
    }

    private Point? GetStoredSize()
    {
        Profile profile = ProfileManager.CurrentProfile;
        int width = Math.Max(MinimumWidth, profile?.NearbyLootGumpWidth ?? DefaultWidth);
        int height = Math.Max(MinimumHeight, profile?.NearbyLootGumpHeight ?? DefaultHeight);
        return new Point(width, height);
    }

    private static void StoreSize(Point? size)
    {
        Profile profile = ProfileManager.CurrentProfile;
        if (profile == null)
            return;

        Point stored = size ?? new Point(DefaultWidth, DefaultHeight);
        profile.NearbyLootGumpWidth = Math.Max(MinimumWidth, stored.X);
        profile.NearbyLootGumpHeight = Math.Max(MinimumHeight, stored.Y);
    }

    private void BuildWindow()
    {
        var root = new Grid { Padding = new Thickness(4) };
        root.ColumnsProportions.Add(new Proportion(ProportionType.Part));
        root.RowsProportions.Add(new Proportion(ProportionType.Auto));
        root.RowsProportions.Add(new Proportion(ProportionType.Part));

        var commands = new Grid
        {
            ColumnSpacing = MyraStyle.STANDARD_SPACING,
            Margin = new Thickness(0, 0, 0, 4)
        };
        commands.RowsProportions.Add(new Proportion(ProportionType.Auto));
        commands.ColumnsProportions.Add(new Proportion(ProportionType.Part));
        commands.ColumnsProportions.Add(new Proportion(ProportionType.Part));
        commands.ColumnsProportions.Add(new Proportion(ProportionType.Part));

        _lootButton = new MyraButton(TazLang.Get("nearbyloot_lootall", "Loot All"), LootAll);
        AddGridWidget(commands, _lootButton, 0, 0);
        AddGridWidget(
            commands,
            new MyraButton(TazLang.Get("nearbyloot_setlootbag", "Set Loot Bag"), SetLootBag),
            0,
            1
        );
        AddGridWidget(
            commands,
            new MyraButton(TazLang.Get("nearbyloot_options", "Options"), ShowOptions),
            0,
            2
        );

        var scrollViewer = new ScrollViewer
        {
            Content = _lootList,
            ShowHorizontalScrollBar = false,
            ShowVerticalScrollBar = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        AddGridWidget(root, commands, 0, 0);
        AddGridWidget(root, scrollViewer, 1, 0);
        SetRootContent(root);
    }

    private static void AddGridWidget(Grid grid, Widget widget, int row, int column)
    {
        grid.Widgets.Add(widget);
        Grid.SetRow(widget, row);
        Grid.SetColumn(widget, column);
    }

    private void SubscribeEvents()
    {
        if (_eventsSubscribed)
            return;

        EventSink.OnCorpseCreated += EventSink_OnCorpseCreated;
        EventSink.OnPositionChanged += EventSink_OnPositionChanged;
        EventSink.OPLOnReceive += EventSink_OPLOnReceive;
        _eventsSubscribed = true;
    }

    private void EventSink_OPLOnReceive(object sender, OPLEventArgs e)
    {
        Item item = _world.Items.Get(e.Serial);
        if (item != null && _openedCorpses.Contains(item.RootContainer))
            RequestUpdateContents();
    }

    private void EventSink_OnPositionChanged(object sender, PositionChangedArgs e) => RequestUpdateContents();

    private void EventSink_OnCorpseCreated(object sender, EventArgs e)
    {
        if (sender is Item { IsDestroyed: false, IsCorpse: true } corpse &&
            corpse.Distance <= ProfileManager.CurrentProfile.AutoOpenCorpseRange)
        {
            TryRequestOpenCorpse(corpse);
        }
    }

    private void ShowOptions()
    {
        Profile profile = ProfileManager.CurrentProfile;
        bool opensHumanCorpses = profile.NearbyLootOpensHumanCorpses;
        bool concealsContainers = profile.NearbyLootConcealsContainerOnOpen;

        ShowContextMenu(
            (
                ContextMenuLabelToggle(
                    opensHumanCorpses,
                    TazLang.Get("nearbyloot_openhumancorpses", "Open human corpses")
                ),
                () =>
                {
                    profile.NearbyLootOpensHumanCorpses = !opensHumanCorpses;
                    RequestUpdateContents();
                }
            ),
            (
                ContextMenuLabelToggle(
                    concealsContainers,
                    TazLang.Get("nearbyloot_hidecontainers", "Hide containers when opening corpses")
                ),
                () => profile.NearbyLootConcealsContainerOnOpen = !concealsContainers
            )
        );
    }

    public void RequestUpdateContents() => _refreshRequested = true;

    private void RebuildLootList()
    {
        string selectedIdentity = _selectedIndex >= 0 && _selectedIndex < _navigationEntries.Count
            ? _navigationEntries[_selectedIndex].Identity
            : null;

        _lootList.Widgets.Clear();
        _navigationEntries.Clear();
        _iconFrames.Clear();
        _lootRows.Clear();
        _visibleLootItems.Clear();
        _openedCorpses.Clear();

        foreach (Item item in _world.Items.Values)
        {
            if (!item.IsDestroyed && item.IsCorpse &&
                item.Distance <= ProfileManager.CurrentProfile.AutoOpenCorpseRange)
            {
                ProcessCorpse(item, _visibleLootItems);
            }
        }

        List<LootGroup> groups = _visibleLootItems
            .GroupBy(item => new LootGroupKey(item.Graphic, item.Hue, NormalizeGroupName(item)))
            .Select(group => new LootGroup(
                group.Key,
                group.OrderBy(item => item.Serial).ToList(),
                GetItemName(group.First(), false)
            ))
            .OrderBy(group => !group.Items[0].IsCoin)
            .ThenBy(group => group.Key.Graphic)
            .ThenBy(group => group.Key.Hue)
            .ThenBy(group => group.Key.NormalizedName, StringComparer.Ordinal)
            .ToList();

        HashSet<LootGroupKey> duplicateKeys = groups
            .Where(group => group.Items.Count > 1)
            .Select(group => group.Key)
            .ToHashSet();
        _expandedGroups.RemoveWhere(key => !duplicateKeys.Contains(key));

        foreach (LootGroup group in groups)
        {
            if (group.Items.Count == 1)
            {
                AddItemRow(group.Items[0], false);
                continue;
            }

            AddGroupHeader(group);
            if (_expandedGroups.Contains(group.Key))
            {
                foreach (Item item in group.Items)
                    AddItemRow(item, true);
            }
        }

        if (_lootList.Widgets.Count == 0)
        {
            _lootList.Widgets.Add(new MyraLabel(
                TazLang.Get("nearbyloot_noitems", "No nearby loot"),
                MyraLabel.TextStyle.P,
                MyraLabel.AlignMode.Center
            ));
        }

        if (selectedIdentity != null)
        {
            int restoredIndex = _navigationEntries.FindIndex(entry => entry.Identity == selectedIdentity);
            _selectedIndex = restoredIndex >= 0 ? restoredIndex : Math.Min(_selectedIndex, _navigationEntries.Count - 1);
        }
        else
        {
            _selectedIndex = Math.Min(_selectedIndex, _navigationEntries.Count - 1);
        }

        RefreshRowVisuals();
    }

    private void ProcessCorpse(Item corpse, List<Item> itemList)
    {
        if (corpse == null || !corpse.IsCorpse ||
            corpse.IsHumanCorpse && !ProfileManager.CurrentProfile.NearbyLootOpensHumanCorpses)
        {
            return;
        }

        if (corpse.Items == null)
        {
            TryRequestOpenCorpse(corpse);
            return;
        }

        corpse.Hue = 53;
        _corpsesRequested.Remove(corpse.Serial);
        _openedCorpses.Add(corpse.Serial);

        for (LinkedObject linked = corpse.Items; linked != null; linked = linked.Next)
        {
            var item = (Item)linked;

            // Corpse objects are containers, not loot. Recurse into them and never add the
            // corpse itself to the visible/lootable list (OSI exposes these as "Corpse").
            if (item.IsCorpse)
            {
                ProcessCorpse(item, itemList);
                continue;
            }

            if (item.Graphic == 0 || !item.IsLootable)
                continue;

            itemList.Add(item);
            GridHighlightData.ProcessItemOpl(_world, item);
        }
    }

    private void TryRequestOpenCorpse(Item corpse)
    {
        if (_openedCorpses.Contains(corpse.Serial) ||
            corpse.Distance > ProfileManager.CurrentProfile.AutoOpenCorpseRange)
        {
            return;
        }

        if (ProfileManager.ServerSettings is { DoNotReopenCorpses: true }
            && CorpseManager.IsCorpseOpened(corpse.Serial))
        {
            return;
        }

        if (ProfileManager.CurrentProfile.NearbyLootConcealsContainerOnOpen)
            _corpsesRequested.Add(corpse.Serial);

        GameActions.QueueOpenCorpse(corpse.Serial);
    }

    private void AddGroupHeader(LootGroup group)
    {
        int entryCount = group.Items.Count;
        int totalAmount = group.Items.Sum(item => Math.Max(1, item.Amount));
        string count = totalAmount == entryCount
            ? FormatLocalized("nearbyloot_groupitems", "{0} items", entryCount)
            : FormatLocalized("nearbyloot_groupstacks", "{0} stacks · {1} total", entryCount, totalAmount);
        string label = $"{group.DisplayName} — {count}";
        string identity = $"group:{group.Key.Graphic:X4}:{group.Key.Hue:X4}:{group.Key.NormalizedName}";
        int index = _navigationEntries.Count;

        NearbyLootRow row = CreateRow(
            group.Items[0],
            label,
            false,
            () => ToggleGroup(group.Key),
            () => SelectRow(index, group.Items[0].Serial),
            () => ClearItemTooltip(group.Items[0].Serial),
            () => group.Items.Any(IsBeingLooted),
            () => GetGroupHighlight(group.Items)
        );

        _navigationEntries.Add(new NavigationEntry(identity, row, () => ToggleGroup(group.Key)));
    }

    private void AddItemRow(Item item, bool indented)
    {
        int index = _navigationEntries.Count;
        string identity = $"item:{item.Serial:X8}";
        NearbyLootRow row = CreateRow(
            item,
            GetItemName(item, true),
            indented,
            () => QuickLoot(item),
            () => SelectRow(index, item.Serial),
            () => ClearItemTooltip(item.Serial),
            () => IsBeingLooted(item),
            () => GetItemHighlight(item)
        );

        _navigationEntries.Add(new NavigationEntry(identity, row, () => QuickLoot(item)));
    }

    private NearbyLootRow CreateRow(
        Item item,
        string label,
        bool indented,
        Action activate,
        Action hover,
        Action leave,
        Func<bool> isBeingLooted,
        Func<Color?> highlightColor
    )
    {
        var icon = new NearbyLootIconFrame(item.DisplayedGraphic, item.Hue, highlightColor);
        var content = new HorizontalStackPanel
        {
            Spacing = MyraStyle.STANDARD_SPACING,
            VerticalAlignment = VerticalAlignment.Center
        };
        content.Widgets.Add(icon);
        content.Widgets.Add(new MyraLabel(label, MyraLabel.TextStyle.P)
        {
            VerticalAlignment = VerticalAlignment.Center
        });

        var row = new NearbyLootRow(activate, hover, leave, isBeingLooted)
        {
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = indented ? new Thickness(18, 0, 0, 0) : new Thickness(0),
            Padding = new Thickness(3, 1)
        };

        _lootList.Widgets.Add(row);
        _iconFrames.Add(icon);
        _lootRows.Add(row);
        return row;
    }

    private static string NormalizeGroupName(Item item) => GetItemName(item, false).Trim().ToUpperInvariant();

    private static string GetItemName(Item item, bool showAmount)
    {
        string name = item?.GetNormalizedName(showAmount);
        return string.IsNullOrWhiteSpace(name)
            ? TazLang.Get("nearbyloot_unknownitem", "Unknown item")
            : name;
    }

    private static string FormatLocalized(string key, string fallback, params object[] values) =>
        string.Format(CultureInfo.CurrentCulture, TazLang.Get(key, fallback), values);

    private void ToggleGroup(LootGroupKey key)
    {
        if (!_expandedGroups.Add(key))
            _expandedGroups.Remove(key);

        RequestUpdateContents();
    }

    private void SelectRow(int index, uint serial)
    {
        _selectedIndex = index;
        SetTooltip(serial);
        RefreshRowVisuals();
    }

    private void ClearItemTooltip(uint serial)
    {
        if (Tooltip is uint tooltipSerial && tooltipSerial == serial)
            ClearTooltip();
    }

    private bool IsBeingLooted(Item item) =>
        item != null && !item.IsDestroyed && AutoLootManager.Instance.IsBeingLooted(item.Serial);

    private static Color? GetItemHighlight(Item item) =>
        item is { IsDestroyed: false, MatchesHighlightData: true } ? item.HighlightColor : null;

    private static Color? GetGroupHighlight(IReadOnlyList<Item> items)
    {
        List<Item> highlighted = items
            .Where(item => item is { IsDestroyed: false, MatchesHighlightData: true })
            .ToList();
        if (highlighted.Count == 0)
            return null;

        foreach (GridHighlightData config in GridHighlightData.AllConfigs)
        {
            if (!config.Enabled)
                continue;

            Item match = highlighted.FirstOrDefault(item =>
                string.Equals(item.HighlightName, config.Name, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return match.HighlightColor;
        }

        return highlighted.OrderBy(item => item.Serial).First().HighlightColor;
    }

    private void QuickLoot(Item item)
    {
        if (item == null || item.IsDestroyed || item.IsCorpse)
            return;

        Profile profile = ProfileManager.CurrentProfile;
        if (Keyboard.Shift && profile.EnableAutoLoot && !profile.HoldShiftForContext && !profile.HoldShiftToSplitStack)
        {
            AutoLootManager.Instance.AddAutoLootEntry(item.Graphic, item.Hue, GetItemName(item, false));
            GameActions.Print(_world, TazLang.Get("nearbyloot_addedautoloot", "Added this item to auto loot."));
        }

        ObjectActionQueueItem action = ObjectActionQueueItem.QuickLoot(item);
        if (action != null)
            ObjectActionQueue.Instance.Enqueue(action, ActionPriority.MoveItem);
    }

    private void LootAll()
    {
        foreach (Item item in _visibleLootItems.ToArray())
        {
            if (item is { IsDestroyed: false, IsCorpse: false })
                AutoLootManager.Instance.LootItem(item.Serial);
        }
    }

    private void SetLootBag()
    {
        GameActions.Print(_world, Resources.ResGumps.TargetContainerToGrabItemsInto);
        _world.TargetManager.SetTargeting(CursorTarget.SetGrabBag, 0, TargetType.Neutral);
    }

    private void MoveSelection(int direction)
    {
        _selectedIndex = Math.Clamp(_selectedIndex + direction, -1, _navigationEntries.Count - 1);
        RefreshRowVisuals();
    }

    private void ActivateSelection()
    {
        if (_selectedIndex < 0)
            LootAll();
        else if (_selectedIndex < _navigationEntries.Count)
            _navigationEntries[_selectedIndex].Activate();
    }

    private void RefreshRowVisuals()
    {
        for (int i = 0; i < _lootRows.Count; i++)
            _lootRows[i].Refresh(i == _selectedIndex);

        bool lootAllSelected = _selectedIndex == -1;
        _lootButton.Border = lootAllSelected ? NearbyLootRow.SelectionBrush : null;
        _lootButton.BorderThickness = lootAllSelected ? new Thickness(1) : new Thickness(0);
    }

    public override void Update()
    {
        base.Update();
        if (IsDisposed)
            return;

        if (_refreshRequested)
        {
            _refreshRequested = false;
            RebuildLootList();
        }

        foreach (NearbyLootIconFrame icon in _iconFrames)
            icon.RefreshHighlight();
        RefreshRowVisuals();

        if (Time.Ticks > _nextClean)
        {
            _openedCorpses.Clear();
            _corpsesRequested.Clear();
            _nextClean = Time.Ticks + CorpseCacheLifetime;
        }
    }

    public override void OnKeyDown(SDL.SDL_Keycode key, SDL.SDL_Keymod mod)
    {
        switch (key)
        {
            case SDL.SDL_Keycode.SDLK_UP:
                MoveSelection(-1);
                break;
            case SDL.SDL_Keycode.SDLK_DOWN:
                MoveSelection(1);
                break;
            case SDL.SDL_Keycode.SDLK_RETURN:
                ActivateSelection();
                break;
        }
    }

    protected override void OnControllerButtonDown(SDL.SDL_GamepadButton button)
    {
        switch (button)
        {
            case SDL.SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_UP:
                MoveSelection(-1);
                break;
            case SDL.SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_DOWN:
                MoveSelection(1);
                break;
            case SDL.SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH:
                ActivateSelection();
                break;
        }
    }

    public static bool IsCorpseRequested(uint serial, bool remove = true)
    {
        if (!_corpsesRequested.Contains(serial))
            return false;

        if (remove)
            _corpsesRequested.Remove(serial);
        return true;
    }

    public override void Dispose()
    {
        if (_eventsSubscribed)
        {
            EventSink.OnCorpseCreated -= EventSink_OnCorpseCreated;
            EventSink.OnPositionChanged -= EventSink_OnPositionChanged;
            EventSink.OPLOnReceive -= EventSink_OPLOnReceive;
            _eventsSubscribed = false;
        }

        _corpsesRequested.Clear();
        _openedCorpses.Clear();
        base.Dispose();
    }

    private readonly record struct LootGroupKey(ushort Graphic, ushort Hue, string NormalizedName);

    private sealed record LootGroup(LootGroupKey Key, List<Item> Items, string DisplayName);

    private sealed record NavigationEntry(string Identity, NearbyLootRow Row, Action Activate);

    private sealed class NearbyLootRow : Button
    {
        internal static readonly SolidBrush SelectionBrush = new(new Color(255, 110, 45, 220));
        private static readonly SolidBrush LootingBrush = new(new Color(50, 180, 80, 220));

        private readonly Action _activate;
        private readonly Action _hover;
        private readonly Action _leave;
        private readonly Func<bool> _isBeingLooted;

        public NearbyLootRow(Action activate, Action hover, Action leave, Func<bool> isBeingLooted)
        {
            _activate = activate;
            _hover = hover;
            _leave = leave;
            _isBeingLooted = isBeingLooted;
            DisabledBackground = Background;
        }

        public void Refresh(bool selected)
        {
            bool beingLooted = _isBeingLooted?.Invoke() == true;
            Border = beingLooted ? LootingBrush : selected ? SelectionBrush : null;
            BorderThickness = beingLooted ? new Thickness(2) : selected ? new Thickness(1) : new Thickness(0);
        }

        public override void OnMouseEntered()
        {
            base.OnMouseEntered();
            _hover?.Invoke();
        }

        public override void OnTouchEntered()
        {
            base.OnTouchEntered();
            _hover?.Invoke();
        }

        public override void OnMouseLeft()
        {
            base.OnMouseLeft();
            _leave?.Invoke();
        }

        public override void OnTouchLeft()
        {
            base.OnTouchLeft();
            _leave?.Invoke();
        }

        public override void OnTouchDown()
        {
            base.OnTouchDown();
            if (Enabled)
                _activate?.Invoke();
        }
    }

    private sealed class NearbyLootIconFrame : Panel
    {
        private readonly Func<Color?> _highlightColor;
        private Color? _lastColor;
        private int _lastThickness = -1;

        public NearbyLootIconFrame(uint graphic, ushort hue, Func<Color?> highlightColor)
        {
            _highlightColor = highlightColor;
            Width = ItemIconSize + 4;
            Height = ItemIconSize + 4;
            Padding = new Thickness(2);
            VerticalAlignment = VerticalAlignment.Center;

            Widgets.Add(new MyraArtTexture(graphic, hue, ItemIconSize)
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            RefreshHighlight();
        }

        public void RefreshHighlight()
        {
            Color? color = _highlightColor?.Invoke();
            int thickness = color.HasValue
                ? Math.Max(1, ProfileManager.CurrentProfile?.GridHighlightSize ?? 1)
                : 0;
            if (_lastColor == color && _lastThickness == thickness)
                return;

            _lastColor = color;
            _lastThickness = thickness;
            Border = color.HasValue ? new SolidBrush(color.Value) : null;
            BorderThickness = new Thickness(thickness);
        }
    }
}
