using ClassicUO.Game.Managers;

namespace ClassicUO.LegionScripting.ApiClasses;

public record struct ApiCombatDpsSnapshot
{
    public uint TargetSerial { get; set; }
    public double MineDps { get; set; }
    public double OthersDps { get; set; }
    public double UnknownDps { get; set; }
    public double TotalDps { get; set; }
    public long MineDamage { get; set; }
    public long OthersDamage { get; set; }
    public long UnknownDamage { get; set; }
    public long TotalDamage { get; set; }
    public int HitCount { get; set; }
    public double ElapsedSeconds { get; set; }
    public double AttributionCoverage { get; set; }
    public bool HasData { get; set; }

    internal static ApiCombatDpsSnapshot FromSnapshot(CombatDamageSnapshot snapshot)
    {
        return new ApiCombatDpsSnapshot
        {
            TargetSerial = snapshot.TargetSerial,
            MineDps = snapshot.MineDps,
            OthersDps = snapshot.OthersDps,
            UnknownDps = snapshot.UnknownDps,
            TotalDps = snapshot.TotalDps,
            MineDamage = snapshot.MineDamage,
            OthersDamage = snapshot.OthersDamage,
            UnknownDamage = snapshot.UnknownDamage,
            TotalDamage = snapshot.TotalDamage,
            HitCount = snapshot.HitCount,
            ElapsedSeconds = snapshot.ElapsedSeconds,
            AttributionCoverage = snapshot.AttributionCoverage,
            HasData = snapshot.HasData
        };
    }
}
