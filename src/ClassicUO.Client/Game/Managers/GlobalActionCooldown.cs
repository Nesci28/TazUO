using ClassicUO.Configuration;
using ClassicUO.Network;

namespace ClassicUO.Game.Managers;

public static class GlobalActionCooldown
{
    private static long _nextActionTime = 0;
    private static long BaseCooldownDuration => ProfileManager.CurrentProfile?.MoveMultiObjectDelay ?? 1000;

    public static long CooldownDuration => BaseCooldownDuration;
    public static long QueuedCooldownDuration => BaseCooldownDuration + CurrentPing;
    public static uint CurrentPing => AsyncNetClient.Socket?.Statistics?.Ping ?? 0;

    public static bool IsOnCooldown => Time.Ticks < _nextActionTime;
    public static void BeginCooldown(bool includePing = false) =>
        _nextActionTime = Time.Ticks + (includePing ? QueuedCooldownDuration : CooldownDuration);
}
