using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ClassicUO.Configuration;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.UI.Gumps.GridHighLight;

namespace ClassicUO.Game.Managers;

/// <summary>Shows grid match alerts once per item and rule during a game session.</summary>
internal static class GridHighlightNotificationManager
{
    private static readonly GridHighlightNotificationTracker Tracker = new();

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
        Tracker.Clear();
    }
}

internal sealed class GridHighlightNotificationTracker
{
    // Item identity permits serial reuse, and weak keys release removed world items.
    private readonly ConditionalWeakTable<Item, HashSet<GridHighlightSetupEntry>> _announced = new();

    internal bool TryAnnounce(Item item, GridHighlightSetupEntry rule) =>
        item != null && rule?.Enabled == true && rule.ShowNotification
        && _announced.GetOrCreateValue(item).Add(rule);

    internal void Clear() => _announced.Clear();
}
