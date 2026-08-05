using System;
using System.Collections.Generic;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;

namespace ClassicUO.Game.Managers;

public sealed class HealthNotificationManager
{
    private readonly HashSet<BuffIconType> _activeDebuffs = [];
    private readonly World _world;
    private bool _healthChanged;
    private bool _loaded;
    private bool _wasLowHealth;

    public static HealthNotificationManager Instance
    {
        get
        {
            if (field == null)
                field = new HealthNotificationManager();

            return field;
        }

        private set => field = value;
    }

    private HealthNotificationManager()
    {
        _world = Client.Game.UO.World;
    }

    public void OnSceneLoad()
    {
        if (_loaded)
            return;

        _loaded = true;
        _healthChanged = false;
        _activeDebuffs.Clear();

        PlayerMobile player = _world.Player;
        HealthNotificationsConfig config = HealthNotificationsConfig.Current;
        _wasLowHealth = player != null && IsLowHealth(player.Hits, player.HitsMax, config.LowHealthPercentage);

        if (player != null)
        {
            foreach (BuffIconType type in player.BuffIcons.Keys)
                _activeDebuffs.Add(type);

            if (player.IsPoisoned)
                _activeDebuffs.Add(BuffIconType.Poison);
        }

        EventSink.OnPlayerHitsChanged += OnPlayerHitsChanged;
        EventSink.OnBuffAddedInternal += OnBuffAdded;
        EventSink.OnBuffRemovedInternal += OnBuffRemoved;
    }

    public void OnSceneUnload()
    {
        EventSink.OnPlayerHitsChanged -= OnPlayerHitsChanged;
        EventSink.OnBuffAddedInternal -= OnBuffAdded;
        EventSink.OnBuffRemovedInternal -= OnBuffRemoved;

        _loaded = false;
        _healthChanged = false;
        _wasLowHealth = false;
        _activeDebuffs.Clear();
        Instance = null;
    }

    public void Update()
    {
        if (!_loaded || !_world.InGame || _world.Player == null)
            return;

        PlayerMobile player = _world.Player;

        if (_healthChanged)
        {
            _healthChanged = false;
            EvaluateLowHealth(player);
        }

        bool poisoned = player.IsPoisoned || player.BuffIcons.ContainsKey(BuffIconType.Poison);
        SetDebuffActive(BuffIconType.Poison, poisoned, true);
    }

    public void TestLowHealthNotification()
    {
        if (!_world.InGame)
            return;

        HealthNotificationsConfig config = HealthNotificationsConfig.Current;
        PlayerMobile player = _world.Player;
        int maxHits = player?.HitsMax > 0 ? player.HitsMax : 100;
        int hits = Math.Max(1, maxHits * config.LowHealthPercentage / 100);
        int healthPercentage = CalculatePercentage(hits, maxHits);

        Show(FormatLowHealth(config.LowHealthAnnouncement, healthPercentage, hits, maxHits));
    }

    public void TestDebuffNotification()
    {
        if (!_world.InGame)
            return;

        HealthNotificationsConfig config = HealthNotificationsConfig.Current;
        BuffIconType type = config.Debuffs is { Count: > 0 } ? config.Debuffs[0] : BuffIconType.Poison;
        Show(FormatDebuff(config.DebuffAnnouncement, HealthDebuffCatalog.GetName(type)));
    }

    private void OnPlayerHitsChanged(object sender, int hits)
    {
        if (_loaded && _world.InGame)
            _healthChanged = true;
    }

    private void EvaluateLowHealth(PlayerMobile player)
    {
        HealthNotificationsConfig config = HealthNotificationsConfig.Current;
        int hits = player.Hits;
        int maxHits = player.HitsMax;
        bool isLowHealth = IsLowHealth(hits, maxHits, config.LowHealthPercentage);

        if (isLowHealth && !_wasLowHealth && config.LowHealthEnabled)
        {
            int healthPercentage = CalculatePercentage(hits, maxHits);
            Show(FormatLowHealth(config.LowHealthAnnouncement, healthPercentage, hits, maxHits));
        }

        _wasLowHealth = isLowHealth;
    }

    private void OnBuffAdded(object sender, BuffEventArgs e)
    {
        if (!_loaded || !_world.InGame || e?.Buff == null)
            return;

        SetDebuffActive(e.Buff.Type, true, true);
    }

    private void OnBuffRemoved(object sender, BuffEventArgs e)
    {
        if (!_loaded || e?.Buff == null)
            return;

        // Poison may be represented by either the buff icon or the mobile flag. Let Update
        // reconcile both sources after the icon has actually been removed.
        if (e.Buff.Type != BuffIconType.Poison)
            _activeDebuffs.Remove(e.Buff.Type);
    }

    private void SetDebuffActive(BuffIconType type, bool active, bool notify)
    {
        if (!active)
        {
            _activeDebuffs.Remove(type);
            return;
        }

        if (!_activeDebuffs.Add(type) || !notify)
            return;

        HealthNotificationsConfig config = HealthNotificationsConfig.Current;

        if (!config.DebuffsEnabled || !config.IsDebuffEnabled(type))
            return;

        Show(FormatDebuff(config.DebuffAnnouncement, HealthDebuffCatalog.GetName(type)));
    }

    private void Show(string message)
    {
        HealthNotificationsConfig config = HealthNotificationsConfig.Current;
        BackpackNotificationManager.Instance.ShowNotification(
            config.Destination,
            message,
            config.Hue,
            config.OnScreenFont,
            config.OnScreenFontSize
        );
    }

    internal static bool IsLowHealth(int hits, int maxHits, int threshold) =>
        maxHits > 0 && Math.Max(0, hits) * 100 <= Math.Clamp(threshold, 1, 100) * maxHits;

    internal static int CalculatePercentage(int hits, int maxHits)
    {
        if (maxHits <= 0)
            return 0;

        return Math.Clamp((int)Math.Round(Math.Max(0, hits) * 100d / maxHits), 0, 100);
    }

    internal static string FormatLowHealth(string template, int percentage, int hits, int maxHits)
    {
        string value = string.IsNullOrWhiteSpace(template)
            ? "Low health: {health}% ({hits}/{maxhits})"
            : template;

        return value
            .Replace("{health}", percentage.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{hits}", hits.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{maxhits}", maxHits.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    internal static string FormatDebuff(string template, string debuff)
    {
        string value = string.IsNullOrWhiteSpace(template) ? "Debuff: {debuff}" : template;
        return value.Replace("{debuff}", debuff, StringComparison.OrdinalIgnoreCase);
    }
}
