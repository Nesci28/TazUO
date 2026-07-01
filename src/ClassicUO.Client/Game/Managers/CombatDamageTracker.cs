using System;
using System.Collections.Generic;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Utility;

namespace ClassicUO.Game.Managers;

public sealed class CombatDamageTracker
{
    private const uint IdleResetMs = 30_000;
    private const uint PruneAfterMs = 60_000;
    private const uint RecentActionMs = 3_500;
    private const uint StrongAttackMs = 650;
    private const uint AttackDecayMs = 2_400;
    private const uint SwingWindowMs = 1_800;
    private const uint SpellWindowMs = 3_000;
    private const uint SpellTargetWindowMs = 2_500;
    private const uint TargetSwapPenaltyMs = 700;
    private const uint SustainedMeleeSessionMs = 12_000;
    private const double SustainedMeleeFloor = 0.85;
    private const double SustainedMeleeRefreshThreshold = 0.55;

    private static readonly TimeSpan DamageWindow = TimeSpan.FromSeconds(15);

    private readonly object _sync = new();
    private readonly Dictionary<uint, TargetDamageState> _targets = new();
    private readonly World _world;

    private IntentRecord _lastAttackIntent;
    private IntentRecord _lastSwingIntent;
    private IntentRecord _lastSpellIntent;
    private IntentRecord _lastSpellVisualIntent;
    private IntentRecord _lastSpellTargetIntent;
    private IntentRecord _lastMeleeSessionIntent;

    private uint _lastTargetSerial;
    private uint _lastTargetChangedAt;
    private uint _lastAnyActivityAt;
    private bool _isSubscribed;

    public CombatDamageTracker(World world)
    {
        _world = world;
    }

    public void OnSceneLoad()
    {
        if (_isSubscribed)
        {
            return;
        }

        EventSink.OnEntityDamage += OnEntityDamage;
        EventSink.OnPlayerDeath += OnPlayerDeath;
        EventSink.OnDisconnected += OnDisconnected;
        EventSink.SpellCastBegin += OnSpellCastBegin;
        _isSubscribed = true;
    }

    public void OnSceneUnload()
    {
        if (!_isSubscribed)
        {
            return;
        }

        EventSink.OnEntityDamage -= OnEntityDamage;
        EventSink.OnPlayerDeath -= OnPlayerDeath;
        EventSink.OnDisconnected -= OnDisconnected;
        EventSink.SpellCastBegin -= OnSpellCastBegin;
        _isSubscribed = false;
        Reset();
    }

    public void Reset()
    {
        lock (_sync)
        {
            _targets.Clear();
            _lastAttackIntent = default;
            _lastSwingIntent = default;
            _lastSpellIntent = default;
            _lastSpellVisualIntent = default;
            _lastSpellTargetIntent = default;
            _lastMeleeSessionIntent = default;
            _lastTargetSerial = 0;
            _lastTargetChangedAt = 0;
            _lastAnyActivityAt = 0;
        }
    }

    public void RecordAttackIntent(uint targetSerial)
    {
        if (!IsValidCombatTarget(targetSerial))
        {
            return;
        }

        lock (_sync)
        {
            uint now = Time.Ticks;
            RefreshTargetAnchor(now);
            _lastAttackIntent = new IntentRecord(targetSerial, now);
            _lastMeleeSessionIntent = new IntentRecord(targetSerial, now);
            _lastAnyActivityAt = now;
            TrackTargetChange(targetSerial, now);
        }
    }

    public void RecordSwing(uint targetSerial)
    {
        if (!IsValidCombatTarget(targetSerial))
        {
            return;
        }

        lock (_sync)
        {
            uint now = Time.Ticks;
            RefreshTargetAnchor(now);
            _lastSwingIntent = new IntentRecord(targetSerial, now);
            _lastMeleeSessionIntent = new IntentRecord(targetSerial, now);
            _lastAnyActivityAt = now;
            TrackTargetChange(targetSerial, now);
        }
    }

    public void RecordSpellIntent(int spellId)
    {
        if (!IsHarmfulSpell(spellId))
        {
            return;
        }

        lock (_sync)
        {
            uint now = Time.Ticks;
            RefreshTargetAnchor(now);
            _lastSpellIntent = new IntentRecord(_world.TargetManager.LastAttack, now);
            _lastAnyActivityAt = now;
        }
    }

    public void RecordHarmfulTargetIntent(uint targetSerial, TargetType targetType)
    {
        if (targetType != TargetType.Harmful || !IsValidCombatTarget(targetSerial))
        {
            return;
        }

        lock (_sync)
        {
            uint now = Time.Ticks;
            RefreshTargetAnchor(now);
            _lastSpellTargetIntent = new IntentRecord(targetSerial, now);
            _lastAnyActivityAt = now;
            TrackTargetChange(targetSerial, now);
        }
    }

    public CombatDamageSnapshot GetActiveSnapshot()
    {
        uint targetSerial = _world.TargetManager.LastAttack;

        lock (_sync)
        {
            if (!IsValidCombatTarget(targetSerial) && IsValidCombatTarget(_lastMeleeSessionIntent.TargetSerial))
            {
                targetSerial = _lastMeleeSessionIntent.TargetSerial;
            }
        }

        return GetSnapshot(targetSerial);
    }

    public CombatDamageSnapshot GetSnapshot(uint targetSerial)
    {
        lock (_sync)
        {
            uint now = Time.Ticks;
            ResetIfIdle(now);
            Prune(now);

            if (!IsValidCombatTarget(targetSerial) || !_targets.TryGetValue(targetSerial, out TargetDamageState state))
            {
                return new CombatDamageSnapshot(targetSerial, 0, 0, 0, 0, 0, 0, 0, false);
            }

            return state.GetSnapshot(now);
        }
    }

    private void OnEntityDamage(object sender, int damage)
    {
        if (sender is Entity entity)
        {
            RecordDamage(entity, damage);
        }
    }

    private void OnPlayerDeath(object sender, uint serial) => Reset();

    private void OnDisconnected(object sender, EventArgs e) => Reset();

    private void OnSpellCastBegin(object sender, int spellId)
    {
        if (!IsHarmfulSpell(spellId))
        {
            return;
        }

        lock (_sync)
        {
            uint now = Time.Ticks;
            RefreshTargetAnchor(now);
            _lastSpellVisualIntent = new IntentRecord(_world.TargetManager.LastAttack, now);
            _lastAnyActivityAt = now;
        }
    }

    private void RecordDamage(Entity entity, int damage)
    {
        if (damage <= 0 || entity == null || !IsValidCombatTarget(entity.Serial))
        {
            return;
        }

        lock (_sync)
        {
            uint now = Time.Ticks;
            RefreshTargetAnchor(now);
            ResetIfIdle(now);

            double probabilityMine = ComputeMineProbability(entity.Serial, now);
            TargetDamageState state = GetOrCreateState(entity.Serial);
            state.AddDamage(now, damage, probabilityMine);
            RefreshSustainedMeleeSession(entity.Serial, now, probabilityMine);
            _lastAnyActivityAt = now;
            Prune(now);
        }
    }

    private double ComputeMineProbability(uint targetSerial, uint now)
    {
        uint anchor = _world.TargetManager.LastAttack;
        bool targetIsLastAttack = targetSerial == anchor;

        double score = targetIsLastAttack ? 0.22 : 0.04;

        score += IntentScore(_lastAttackIntent, targetSerial, now, StrongAttackMs, AttackDecayMs, 0.38);
        score += IntentScore(_lastSwingIntent, targetSerial, now, 0, SwingWindowMs, 0.46);
        score += IntentScore(_lastSpellIntent, targetSerial, now, 0, SpellWindowMs, 0.24);
        score += IntentScore(_lastSpellVisualIntent, targetSerial, now, 0, SpellWindowMs, 0.12);
        score += IntentScore(_lastSpellTargetIntent, targetSerial, now, 0, SpellTargetWindowMs, 0.30);

        bool hasRecentTargetAction = HasRecentTargetAction(targetSerial, now);
        bool hasSustainedMeleeSession = HasSustainedMeleeSession(targetSerial, now);

        if (!hasRecentTargetAction)
        {
            score *= targetIsLastAttack ? 0.55 : 0.25;
        }

        if (!targetIsLastAttack && !hasRecentTargetAction && !hasSustainedMeleeSession)
        {
            score *= 0.45;
        }

        if (_lastTargetChangedAt != 0 && Elapsed(now, _lastTargetChangedAt) <= TargetSwapPenaltyMs && !hasRecentTargetAction)
        {
            score *= 0.6;
        }

        if (_lastAnyActivityAt == 0 || Elapsed(now, _lastAnyActivityAt) > RecentActionMs)
        {
            score *= 0.5;
        }

        if (hasSustainedMeleeSession)
        {
            score = Math.Max(score, SustainedMeleeFloor);
        }

        return Math.Clamp(score, 0.02, 0.98);
    }

    private double IntentScore(IntentRecord intent, uint targetSerial, uint now, uint strongMs, uint decayMs, double weight)
    {
        if (!intent.IsSet || !IntentMatches(intent, targetSerial))
        {
            return 0;
        }

        uint elapsed = Elapsed(now, intent.Tick);

        if (elapsed > decayMs)
        {
            return 0;
        }

        if (strongMs > 0 && elapsed <= strongMs)
        {
            return weight;
        }

        uint decayStart = strongMs;
        uint decayDuration = Math.Max(1u, decayMs - decayStart);
        double remaining = 1.0 - Math.Min(1.0, (double)(elapsed - decayStart) / decayDuration);

        return weight * remaining;
    }

    private bool IntentMatches(IntentRecord intent, uint targetSerial)
    {
        return intent.TargetSerial == targetSerial
               || (!IsValidCombatTarget(intent.TargetSerial) && targetSerial == _world.TargetManager.LastAttack);
    }

    private bool HasRecentTargetAction(uint targetSerial, uint now)
    {
        return HasRecentAction(_lastAttackIntent, targetSerial, now)
               || HasRecentAction(_lastSwingIntent, targetSerial, now)
               || HasRecentAction(_lastSpellIntent, targetSerial, now)
               || HasRecentAction(_lastSpellVisualIntent, targetSerial, now)
               || HasRecentAction(_lastSpellTargetIntent, targetSerial, now);
    }

    private bool HasRecentAction(IntentRecord intent, uint targetSerial, uint now)
    {
        return intent.IsSet
               && IntentMatches(intent, targetSerial)
               && Elapsed(now, intent.Tick) <= RecentActionMs;
    }

    private bool HasSustainedMeleeSession(uint targetSerial, uint now)
    {
        return _lastMeleeSessionIntent.IsSet
               && _lastMeleeSessionIntent.TargetSerial == targetSerial
               && _world.Player?.InWarMode == true
               && Elapsed(now, _lastMeleeSessionIntent.Tick) <= SustainedMeleeSessionMs;
    }

    private void RefreshSustainedMeleeSession(uint targetSerial, uint now, double probabilityMine)
    {
        if (probabilityMine < SustainedMeleeRefreshThreshold || !HasSustainedMeleeSession(targetSerial, now))
        {
            return;
        }

        _lastMeleeSessionIntent = new IntentRecord(targetSerial, now);
    }

    private void RefreshTargetAnchor(uint now)
    {
        TrackTargetChange(_world.TargetManager.LastAttack, now);
    }

    private void TrackTargetChange(uint targetSerial, uint now)
    {
        if (_lastTargetSerial != targetSerial)
        {
            _lastTargetSerial = targetSerial;
            _lastTargetChangedAt = now;
        }
    }

    private TargetDamageState GetOrCreateState(uint targetSerial)
    {
        if (!_targets.TryGetValue(targetSerial, out TargetDamageState state))
        {
            state = new TargetDamageState(targetSerial);
            _targets[targetSerial] = state;
        }

        return state;
    }

    private void ResetIfIdle(uint now)
    {
        if (_lastAnyActivityAt != 0 && Elapsed(now, _lastAnyActivityAt) > IdleResetMs)
        {
            _targets.Clear();
            _lastAttackIntent = default;
            _lastSwingIntent = default;
            _lastSpellIntent = default;
            _lastSpellVisualIntent = default;
            _lastSpellTargetIntent = default;
            _lastMeleeSessionIntent = default;
            _lastTargetSerial = _world.TargetManager.LastAttack;
            _lastTargetChangedAt = now;
            _lastAnyActivityAt = 0;
        }
    }

    private void Prune(uint now)
    {
        List<uint> remove = null;

        foreach ((uint serial, TargetDamageState state) in _targets)
        {
            if (Elapsed(now, state.LastDamageTick) > PruneAfterMs)
            {
                remove ??= new List<uint>();
                remove.Add(serial);
            }
        }

        if (remove == null)
        {
            return;
        }

        foreach (uint serial in remove)
        {
            _targets.Remove(serial);
        }
    }

    private bool IsHarmfulSpell(int spellId)
    {
        return SpellDefinition.FullIndexGetSpell(spellId)?.TargetType == TargetType.Harmful;
    }

    private bool IsValidCombatTarget(uint serial)
    {
        PlayerMobile player = _world.Player;
        return SerialHelper.IsValid(serial) && (player == null || serial != player.Serial);
    }

    private static uint Elapsed(uint now, uint then) => now - then;

    private readonly record struct IntentRecord(uint TargetSerial, uint Tick)
    {
        public bool IsSet => Tick != 0;
    }

    private sealed class TargetDamageState
    {
        private readonly AverageOverTime _mine = new(DamageWindow);
        private readonly AverageOverTime _others = new(DamageWindow);
        private readonly AverageOverTime _total = new(DamageWindow);
        private readonly AverageOverTime _confidence = new(DamageWindow);

        public TargetDamageState(uint serial)
        {
            Serial = serial;
        }

        public uint Serial { get; }
        public uint LastDamageTick { get; private set; }

        public void AddDamage(uint now, int damage, double probabilityMine)
        {
            double mine = damage * probabilityMine;
            _mine.AddValue(now, mine);
            _others.AddValue(now, damage - mine);
            _total.AddValue(now, damage);
            _confidence.AddValue(now, probabilityMine);
            LastDamageTick = now;
        }

        public CombatDamageSnapshot GetSnapshot(uint now)
        {
            return new CombatDamageSnapshot(
                Serial,
                Math.Round(_mine.AveragePerSecond(now), 1),
                Math.Round(_others.AveragePerSecond(now), 1),
                Math.Round(_total.AveragePerSecond(now), 1),
                Math.Round(_mine.Sum(now), 1),
                Math.Round(_others.Sum(now), 1),
                Math.Round(_total.Sum(now), 1),
                Math.Round(_confidence.Average(now), 2),
                LastDamageTick != 0
            );
        }
    }
}
