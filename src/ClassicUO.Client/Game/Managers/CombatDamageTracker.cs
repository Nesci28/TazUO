using System;
using System.Collections.Generic;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;

namespace ClassicUO.Game.Managers;

/// <summary>
/// Tracks damage observed by the client and attributes whole damage events only when a matching
/// source event is available. Damage packets contain the target and amount, but not the attacker.
/// </summary>
public sealed class CombatDamageTracker
{
    private const uint IdleResetMs = 30_000;
    private const uint PruneAfterMs = 60_000;
    private const uint SwingEvidenceMs = 1_200;
    private const uint SpellEvidenceMs = 3_500;

    private readonly object _sync = new();
    private readonly Dictionary<uint, TargetDamageState> _targets = new();
    private readonly Dictionary<uint, List<SourceEvidence>> _sourceEvidence = new();
    private readonly World _world;

    private uint _activeTargetSerial;
    private uint _lastDamageAt;
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
        _isSubscribed = false;
        Reset();
    }

    public void Reset()
    {
        lock (_sync)
        {
            _targets.Clear();
            _sourceEvidence.Clear();
            _activeTargetSerial = 0;
            _lastDamageAt = 0;
        }
    }

    /// <summary>
    /// Records a server swing event. The swing packet identifies both attacker and defender.
    /// </summary>
    public void RecordSwing(uint attackerSerial, uint targetSerial)
    {
        if (!IsValidCombatTarget(targetSerial) || !SerialHelper.IsValid(attackerSerial))
        {
            return;
        }

        DamageSource source = attackerSerial == _world.Player?.Serial
            ? DamageSource.Mine
            : DamageSource.Others;

        lock (_sync)
        {
            if (source == DamageSource.Mine)
            {
                _activeTargetSerial = targetSerial;
            }

            AddEvidence(targetSerial, source, Time.Ticks, SwingEvidenceMs);
        }
    }

    /// <summary>
    /// Records an outgoing harmful target selection from this client.
    /// </summary>
    public void RecordHarmfulTargetIntent(uint targetSerial, TargetType targetType)
    {
        if (targetType != TargetType.Harmful || !IsValidCombatTarget(targetSerial))
        {
            return;
        }

        lock (_sync)
        {
            _activeTargetSerial = targetSerial;
            AddEvidence(targetSerial, DamageSource.Mine, Time.Ticks, SpellEvidenceMs);
        }
    }

    public CombatDamageSnapshot GetActiveSnapshot()
    {
        lock (_sync)
        {
            uint now = Time.Ticks;
            ResetIfIdle(now);
            Prune(now);

            uint targetSerial = _activeTargetSerial;

            if (!IsValidCombatTarget(targetSerial) || !_targets.ContainsKey(targetSerial))
            {
                targetSerial = _world.TargetManager.LastAttack;

                if (!IsValidCombatTarget(targetSerial) || !_targets.ContainsKey(targetSerial))
                {
                    targetSerial = GetMostRecentTarget(now);
                }
            }

            return GetSnapshotLocked(targetSerial);
        }
    }

    public CombatDamageSnapshot GetSnapshot(uint targetSerial)
    {
        lock (_sync)
        {
            uint now = Time.Ticks;
            ResetIfIdle(now);
            Prune(now);
            return GetSnapshotLocked(targetSerial);
        }
    }

    private CombatDamageSnapshot GetSnapshotLocked(uint targetSerial)
    {
        if (!IsValidCombatTarget(targetSerial) || !_targets.TryGetValue(targetSerial, out TargetDamageState state))
        {
            return new CombatDamageSnapshot(targetSerial, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false);
        }

        return state.GetSnapshot();
    }

    private void OnEntityDamage(object sender, int damage)
    {
        if (sender is not Entity entity || damage <= 0 || !IsValidCombatTarget(entity.Serial))
        {
            return;
        }

        lock (_sync)
        {
            uint now = Time.Ticks;
            ResetIfIdle(now);

            DamageSource source = ResolveSource(entity.Serial, now);

            if (source == DamageSource.Mine)
            {
                _activeTargetSerial = entity.Serial;
            }

            if (!_targets.TryGetValue(entity.Serial, out TargetDamageState state))
            {
                state = new TargetDamageState(entity.Serial);
                _targets[entity.Serial] = state;
            }

            state.AddDamage(now, damage, source);
            _lastDamageAt = now;
            Prune(now);
        }
    }

    private void OnPlayerDeath(object sender, uint serial) => Reset();

    private void OnDisconnected(object sender, EventArgs e) => Reset();

    private void AddEvidence(uint targetSerial, DamageSource source, uint now, uint lifetimeMs)
    {
        if (!_sourceEvidence.TryGetValue(targetSerial, out List<SourceEvidence> evidence))
        {
            evidence = new List<SourceEvidence>();
            _sourceEvidence[targetSerial] = evidence;
        }

        RemoveExpiredEvidence(evidence, now);
        evidence.Add(new SourceEvidence(source, now, lifetimeMs));
    }

    private DamageSource ResolveSource(uint targetSerial, uint now)
    {
        if (!_sourceEvidence.TryGetValue(targetSerial, out List<SourceEvidence> evidence))
        {
            return DamageSource.Unknown;
        }

        RemoveExpiredEvidence(evidence, now);

        if (evidence.Count == 0)
        {
            _sourceEvidence.Remove(targetSerial);
            return DamageSource.Unknown;
        }

        DamageSource source = evidence[0].Source;
        bool hasConflictingSource = false;
        int bestIndex = 0;
        uint bestElapsed = uint.MaxValue;

        for (int i = 0; i < evidence.Count; i++)
        {
            if (evidence[i].Source != source)
            {
                hasConflictingSource = true;
            }

            uint elapsed = Elapsed(now, evidence[i].Tick);

            if (elapsed < bestElapsed)
            {
                bestElapsed = elapsed;
                bestIndex = i;
            }
        }

        if (hasConflictingSource)
        {
            _sourceEvidence.Remove(targetSerial);
            return DamageSource.Unknown;
        }

        evidence.RemoveAt(bestIndex);

        if (evidence.Count == 0)
        {
            _sourceEvidence.Remove(targetSerial);
        }

        return source;
    }

    private static void RemoveExpiredEvidence(List<SourceEvidence> evidence, uint now)
    {
        for (int i = evidence.Count - 1; i >= 0; i--)
        {
            if (Elapsed(now, evidence[i].Tick) > evidence[i].LifetimeMs)
            {
                evidence.RemoveAt(i);
            }
        }
    }

    private uint GetMostRecentTarget(uint now)
    {
        uint targetSerial = 0;
        uint shortestElapsed = uint.MaxValue;

        foreach ((uint serial, TargetDamageState state) in _targets)
        {
            uint elapsed = Elapsed(now, state.LastDamageTick);

            if (elapsed < shortestElapsed)
            {
                shortestElapsed = elapsed;
                targetSerial = serial;
            }
        }

        return targetSerial;
    }

    private void ResetIfIdle(uint now)
    {
        if (_targets.Count > 0 && Elapsed(now, _lastDamageAt) > IdleResetMs)
        {
            _targets.Clear();
            _sourceEvidence.Clear();
            _activeTargetSerial = 0;
            _lastDamageAt = 0;
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
            remove = new List<uint>();
        }

        foreach (uint serial in remove)
        {
            _targets.Remove(serial);
            _sourceEvidence.Remove(serial);
        }

        remove.Clear();

        foreach ((uint serial, List<SourceEvidence> evidence) in _sourceEvidence)
        {
            RemoveExpiredEvidence(evidence, now);

            if (evidence.Count == 0)
            {
                remove.Add(serial);
            }
        }

        foreach (uint serial in remove)
        {
            _sourceEvidence.Remove(serial);
        }
    }

    private bool IsValidCombatTarget(uint serial)
    {
        PlayerMobile player = _world.Player;
        return SerialHelper.IsValid(serial) && (player == null || serial != player.Serial);
    }

    private static uint Elapsed(uint now, uint then) => unchecked(now - then);

    private enum DamageSource
    {
        Unknown,
        Mine,
        Others
    }

    private readonly record struct SourceEvidence(DamageSource Source, uint Tick, uint LifetimeMs);

    private sealed class TargetDamageState
    {
        private long _mineDamage;
        private long _othersDamage;
        private long _unknownDamage;

        public TargetDamageState(uint serial)
        {
            Serial = serial;
        }

        public uint Serial { get; }
        public uint FirstDamageTick { get; private set; }
        public uint LastDamageTick { get; private set; }
        public int HitCount { get; private set; }

        public void AddDamage(uint now, int damage, DamageSource source)
        {
            if (HitCount > 0 && Elapsed(now, LastDamageTick) > IdleResetMs)
            {
                _mineDamage = 0;
                _othersDamage = 0;
                _unknownDamage = 0;
                HitCount = 0;
            }

            if (HitCount == 0)
            {
                FirstDamageTick = now;
            }

            switch (source)
            {
                case DamageSource.Mine:
                    _mineDamage += damage;
                    break;
                case DamageSource.Others:
                    _othersDamage += damage;
                    break;
                default:
                    _unknownDamage += damage;
                    break;
            }

            HitCount++;
            LastDamageTick = now;
        }

        public CombatDamageSnapshot GetSnapshot()
        {
            if (HitCount == 0)
            {
                return new CombatDamageSnapshot(Serial, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false);
            }

            long totalDamage = _mineDamage + _othersDamage + _unknownDamage;
            double elapsedSeconds = Elapsed(LastDamageTick, FirstDamageTick) / 1_000.0;
            double rateSeconds = elapsedSeconds > 0 ? elapsedSeconds : 1.0;
            long attributedDamage = _mineDamage + _othersDamage;
            double attributionCoverage = totalDamage > 0 ? (double)attributedDamage / totalDamage : 0;

            return new CombatDamageSnapshot(
                Serial,
                Math.Round(_mineDamage / rateSeconds, 1),
                Math.Round(_othersDamage / rateSeconds, 1),
                Math.Round(_unknownDamage / rateSeconds, 1),
                Math.Round(totalDamage / rateSeconds, 1),
                _mineDamage,
                _othersDamage,
                _unknownDamage,
                totalDamage,
                HitCount,
                Math.Round(elapsedSeconds, 1),
                Math.Round(attributionCoverage, 2),
                true
            );
        }
    }
}
