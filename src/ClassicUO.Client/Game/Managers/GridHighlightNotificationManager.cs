using System.Collections.Generic;
using ClassicUO.Configuration;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.UI.Gumps.GridHighLight;

namespace ClassicUO.Game.Managers;

/// <summary>Shows grid match alerts for newly discovered corpse loot during a game session.</summary>
internal static class GridHighlightNotificationManager
{
    private static readonly GridHighlightNotificationTracker Tracker = new();

    public static void OnSceneLoad()
    {
        Clear();
        EventSink.OnItemAddedToContainerInternal += OnItemAddedToContainer;
    }

    private static void OnItemAddedToContainer(object sender, ItemContainerUpdateEventArgs e)
    {
        if (e.Item?.World.InGame != true || !e.Item.World.ClientFeatures.TooltipsEnabled)
            return;

        // Capture the source before auto-looting or later container refreshes replace the item.
        ObserveItem(e.Item);
        if (Tracker.HasPendingNotification(e.Item.Serial))
        {
            if (e.Item.IsLootable)
                GridHighlightData.ProcessItemOpl(e.Item.World, e.Item);
            else
                CompleteItem(e.Item.Serial);
        }
    }

    public static void ObserveItem(Item item, bool allowNotification = true) =>
        Tracker.ObserveItem(item, GridHighlightsConfig.Current.Highlights, allowNotification);

    public static void CompleteItem(uint serial) => Tracker.CompleteItem(serial);

    public static void SuppressPendingNotifications() => Tracker.SuppressPendingNotifications();

    public static void NotifyMatch(World world, Item item, GridHighlightSetupEntry rule)
    {
        if (!world.InGame || !Tracker.TryAnnounce(item, rule))
            return;

        string itemName = item.GetNormalizedName(false);
        if (string.IsNullOrWhiteSpace(itemName))
            itemName = $"0x{item.Graphic:X4}";

        Show(world, rule, itemName);
    }

    public static void TestNotification(World world, GridHighlightSetupEntry rule) =>
        Show(world, rule, TazLang.Get("gridhighlightnotifications_testitem"));

    private static void Show(World world, GridHighlightSetupEntry rule, string itemName)
    {
        if (world?.InGame != true || rule == null)
            return;

        GridHighlightNotificationSettings settings = rule.Notification ?? new();
        string message = FormatAnnouncement(rule, itemName);
        BackpackNotificationManager.Instance.ShowNotification(
            settings.Destination, message, settings.Hue, settings.OnScreenFont, settings.OnScreenFontSize);
    }

    internal static string GetRuleName(GridHighlightSetupEntry rule) =>
        string.IsNullOrWhiteSpace(rule.Name) ? TazLang.Get("gridhighlight_unnamedrule") : rule.Name.Trim();

    internal static string FormatAnnouncement(GridHighlightSetupEntry rule, string itemName)
    {
        string template = rule.Notification?.Announcement;
        if (string.IsNullOrWhiteSpace(template))
            template = "{rule} matched: {item}";

        return BackpackNotificationManager.FormatAnnouncement(template, GetRuleName(rule), itemName ?? string.Empty);
    }

    public static void Clear()
    {
        EventSink.OnItemAddedToContainerInternal -= OnItemAddedToContainer;
        Tracker.Clear();
    }
}

internal sealed class GridHighlightNotificationTracker
{
    // A container reload or transfer can create a new Item for the same server serial.
    // Remember observations for the session, and keep rule references only until properties arrive.
    private readonly HashSet<uint> _seenItems = new();
    private readonly Dictionary<uint, HashSet<GridHighlightSetupEntry>> _pendingRules = new();

    internal void ObserveItem(Item item, IEnumerable<GridHighlightSetupEntry> rules, bool allowNotification = true)
    {
        if (item == null || item.IsDestroyed || item.OnGround || item.IsMulti)
            return;

        if (!allowNotification)
        {
            _seenItems.Add(item.Serial);
            CompleteItem(item.Serial);
            return;
        }

        uint rootSerial = item.RootContainer;
        if (rootSerial == 0 || !_seenItems.Add(item.Serial))
            return;

        Item root = item.World.Items.Get(rootSerial);
        if (root?.IsCorpse != true)
            return;

        var enabledRules = new HashSet<GridHighlightSetupEntry>();
        foreach (GridHighlightSetupEntry rule in rules)
        {
            if (rule.Enabled && rule.ShowNotification)
                enabledRules.Add(rule);
        }

        if (enabledRules.Count > 0)
            _pendingRules.Add(item.Serial, enabledRules);
    }

    internal bool HasPendingNotification(uint serial) => _pendingRules.ContainsKey(serial);

    internal bool TryAnnounce(Item item, GridHighlightSetupEntry rule) =>
        item != null && rule?.Enabled == true && rule.ShowNotification
        && _pendingRules.TryGetValue(item.Serial, out HashSet<GridHighlightSetupEntry> rules)
        && rules.Remove(rule);

    internal void CompleteItem(uint serial) => _pendingRules.Remove(serial);

    internal void SuppressPendingNotifications() => _pendingRules.Clear();

    internal void Clear()
    {
        _seenItems.Clear();
        _pendingRules.Clear();
    }
}
