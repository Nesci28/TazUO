namespace ClassicUO.Game.Managers;

public readonly record struct CombatDamageSnapshot(
    uint TargetSerial,
    double MineDps,
    double OthersDps,
    double UnknownDps,
    double TotalDps,
    long MineDamage,
    long OthersDamage,
    long UnknownDamage,
    long TotalDamage,
    int HitCount,
    double ElapsedSeconds,
    double AttributionCoverage,
    bool HasData
);
