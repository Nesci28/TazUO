using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.UI;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Utility;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Managers;

public sealed class BackpackNotificationManager
{
    private const long PendingPropertyTimeout = 10000;
    private const int OnScreenWidth = 360;
    private const int OnScreenTopOffset = 120;
    private const int OnScreenSpacing = 5;

    private readonly Dictionary<uint, long> _pendingPropertySerials = [];
    private readonly HashSet<string> _announcedRuleSerials = [];
    private readonly List<SimpleTimedTextGump> _onScreenMessages = [];
    private readonly World _world;
    private bool _loaded;

    public static BackpackNotificationManager Instance
    {
        get
        {
            if (field == null)
                field = new BackpackNotificationManager();

            return field;
        }

        private set => field = value;
    }

    private BackpackNotificationManager()
    {
        _world = Client.Game.UO.World;
    }

    public void OnSceneLoad()
    {
        _loaded = true;
        _pendingPropertySerials.Clear();
        _announcedRuleSerials.Clear();
        _onScreenMessages.Clear();

        EventSink.OnItemAddedToContainerInternal += OnItemAddedToContainer;
        EventSink.OPLOnReceive += OnOPLReceived;
    }

    public void OnSceneUnload()
    {
        EventSink.OnItemAddedToContainerInternal -= OnItemAddedToContainer;
        EventSink.OPLOnReceive -= OnOPLReceived;

        _loaded = false;
        _pendingPropertySerials.Clear();
        _announcedRuleSerials.Clear();
        _onScreenMessages.Clear();
        Instance = null;
    }

    public void Update()
    {
        if (!_loaded || !_world.InGame || _pendingPropertySerials.Count == 0)
            return;

        List<uint> expired = null;

        foreach ((uint serial, long expiresAt) in _pendingPropertySerials)
        {
            Item item = _world.Items.Get(serial);

            if (item == null || !IsInPlayerBackpack(item) || Time.Ticks > expiresAt)
            {
                (expired ??= []).Add(serial);
                continue;
            }

            if (HasPropertyText(item))
            {
                Evaluate(item);
                (expired ??= []).Add(serial);
            }
        }

        if (expired == null)
            return;

        foreach (uint serial in expired)
            _pendingPropertySerials.Remove(serial);
    }

    private void OnItemAddedToContainer(object sender, ItemContainerUpdateEventArgs e)
    {
        if (!_loaded || !_world.InGame || e.Item == null)
            return;

        if (!e.ItemWasCreated || e.IsBulkUpdate || !IsPlayerBackpack(e.ContainerSerial))
            return;

        Evaluate(e.Item);

        if (NeedsPropertyText(e.Item))
        {
            _pendingPropertySerials[e.Item.Serial] = Time.Ticks + PendingPropertyTimeout;
            _world.OPL.Contains(e.Item.Serial);
        }
    }

    private void OnOPLReceived(object sender, OPLEventArgs e)
    {
        if (!_loaded || !_world.InGame || !_pendingPropertySerials.ContainsKey(e.Serial))
            return;

        Item item = _world.Items.Get(e.Serial);

        if (item == null || !IsInPlayerBackpack(item))
        {
            _pendingPropertySerials.Remove(e.Serial);
            return;
        }

        item.OPLName = e.Name;
        item.OPLData = e.Data;
        Evaluate(item);
        _pendingPropertySerials.Remove(e.Serial);
    }

    private void Evaluate(Item item)
    {
        foreach (BackpackNotificationConfigEntry rule in BackpackNotificationsConfig.Current.Rules)
        {
            if (!Matches(rule, item))
                continue;

            string key = $"{item.Serial:X8}:{rule.Name}:{rule.Graphic}:{rule.Hues}:{rule.RegexSearch}:{rule.Announcement}";

            if (!_announcedRuleSerials.Add(key))
                continue;

            Notify(rule, item);
        }
    }

    private bool Matches(BackpackNotificationConfigEntry rule, Item item)
    {
        if (rule == null || !rule.Enabled)
            return false;

        if (rule.Graphic != -1 && rule.Graphic != item.Graphic)
            return false;

        if (!HueMatches(rule.Hues, item.Hue))
            return false;

        if (string.IsNullOrWhiteSpace(rule.RegexSearch))
            return true;

        if (!HasPropertyText(item))
            return false;

        try
        {
            return RegexHelper
                .GetRegex(rule.RegexSearch, RegexOptions.Multiline | RegexOptions.IgnoreCase)
                .IsMatch(GetSearchText(item));
        }
        catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
        {
            Log.Warn($"Invalid backpack notification regex '{rule.RegexSearch}': {ex.Message}");
            return false;
        }
    }

    private bool NeedsPropertyText(Item item)
    {
        if (HasPropertyText(item))
            return false;

        foreach (BackpackNotificationConfigEntry rule in BackpackNotificationsConfig.Current.Rules)
        {
            if (rule?.Enabled != true || string.IsNullOrWhiteSpace(rule.RegexSearch))
                continue;

            if (rule.Graphic != -1 && rule.Graphic != item.Graphic)
                continue;

            if (!HueMatches(rule.Hues, item.Hue))
                continue;

            return true;
        }

        return false;
    }

    private static bool HasPropertyText(Item item) =>
        item.OPLName.NotNullNotEmpty() || item.OPLData.NotNullNotEmpty();

    private static string GetSearchText(Item item)
    {
        string name = item.OPLName.NotNullNotEmpty() ? item.OPLName : item.GetNormalizedName(false);

        if (item.OPLData.NotNullNotEmpty())
            return $"{name}\n{item.OPLData}";

        return name ?? string.Empty;
    }

    private bool IsPlayerBackpack(uint containerSerial)
    {
        Item backpack = _world.Player?.Backpack;
        return backpack != null && backpack.Serial == containerSerial;
    }

    private bool IsInPlayerBackpack(Item item) => IsPlayerBackpack(item.Container);

    private void Notify(BackpackNotificationConfigEntry rule, Item item)
    {
        string itemName = item.GetNormalizedName(false);

        if (string.IsNullOrWhiteSpace(itemName))
            itemName = $"0x{item.Graphic:X4}";

        string ruleName = string.IsNullOrWhiteSpace(rule.Name)
            ? TazLang.Get("backpacknotifications_defaultname", "Item")
            : rule.Name.Trim();

        string message = FormatAnnouncement(rule, ruleName, itemName);

        if (rule.Journal)
            GameActions.Print(_world, message, rule.JournalHue, MessageType.System);

        if (rule.Overhead && _world.Player != null)
            GameActions.MessageOverhead(_world, message, rule.OverheadHue, _world.Player.Serial);

        if (rule.OnScreen)
            AddOnScreenMessage(message, rule.OnScreenHue);
    }

    private void AddOnScreenMessage(string message, ushort hue)
    {
        var timedText = new SimpleTimedTextGump(_world, message, hue, TimeSpan.FromSeconds(3), OnScreenWidth);
        timedText.CenterXInViewPort();
        timedText.Y = Client.Game.Scene.Camera.Bounds.Y + OnScreenTopOffset;

        _onScreenMessages.RemoveAll(g => g == null || g.IsDisposed);

        foreach (SimpleTimedTextGump existing in _onScreenMessages)
            existing.Y += timedText.Height + OnScreenSpacing;

        _onScreenMessages.Insert(0, timedText);
        UIManager.Add(timedText);
    }

    private static bool HueMatches(string hues, ushort itemHue)
    {
        if (string.IsNullOrWhiteSpace(hues))
            return true;

        string[] tokens = hues.Split([',', ';', ' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries);

        foreach (string token in tokens)
        {
            string trimmed = token.Trim();

            if (trimmed == "-1")
                return true;

            if (StringHelper.TryParseInt(trimmed, out int hue) && hue == itemHue)
                return true;
        }

        return false;
    }

    private static string FormatAnnouncement(BackpackNotificationConfigEntry rule, string ruleName, string itemName)
    {
        string template = string.IsNullOrWhiteSpace(rule.Announcement)
            ? TazLang.Get("backpacknotifications_receivedtemplate", "{rule} received: {item}")
            : rule.Announcement;

        if (template.Contains("{0}", StringComparison.Ordinal) || template.Contains("{1}", StringComparison.Ordinal))
        {
            try
            {
                return string.Format(template, ruleName, itemName);
            }
            catch (FormatException)
            {
                // Fall through to named token replacement below.
            }
        }

        return template
            .Replace("{rule}", ruleName, StringComparison.OrdinalIgnoreCase)
            .Replace("{item}", itemName, StringComparison.OrdinalIgnoreCase);
    }
}
