using ClassicUO.Game.Managers;

namespace ClassicUO.LegionScripting.ApiClasses;

public record struct ApiCombatDpsSnapshot
{
    public uint TargetSerial { get; set; }
    public double MineDps { get; set; }
    public double OthersDps { get; set; }
    public double TotalDps { get; set; }
    public double MineDamage { get; set; }
    public double OthersDamage { get; set; }
    public double TotalDamage { get; set; }
    public double Confidence { get; set; }
    public bool HasData { get; set; }

    internal static ApiCombatDpsSnapshot FromSnapshot(CombatDamageSnapshot snapshot)
    {
        return new ApiCombatDpsSnapshot
        {
            TargetSerial = snapshot.TargetSerial,
            MineDps = snapshot.MineDps,
            OthersDps = snapshot.OthersDps,
            TotalDps = snapshot.TotalDps,
            MineDamage = snapshot.MineDamage,
            OthersDamage = snapshot.OthersDamage,
            TotalDamage = snapshot.TotalDamage,
            Confidence = snapshot.Confidence,
            HasData = snapshot.HasData
        };
    }
}
