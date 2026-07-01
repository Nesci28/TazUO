namespace ClassicUO.Game.Managers;

public readonly record struct CombatDamageSnapshot(
    uint TargetSerial,
    double MineDps,
    double OthersDps,
    double TotalDps,
    double MineDamage,
    double OthersDamage,
    double TotalDamage,
    double Confidence,
    bool HasData
);
