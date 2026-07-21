using ClassicUO.Configuration;
using ClassicUO.Network;

namespace ClassicUO.Game.Managers;

public static class GlobalActionCooldown
{
    public const uint MaxNetworkSafetyMargin = 50;

    private static long _nextActionTime = 0;
    private static long BaseCooldownDuration => ProfileManager.CurrentProfile?.MoveMultiObjectDelay ?? 1000;

    public static long CooldownDuration => BaseCooldownDuration;
    public static long QueuedCooldownDuration
    {
        get
        {
            uint ping = CurrentPing;
            return BaseCooldownDuration + ping + GetNetworkSafetyMargin(ping);
        }
    }

    public static uint CurrentPing => AsyncNetClient.Socket?.Statistics?.Ping ?? 0;

    public static uint GetNetworkSafetyMargin(uint ping)
    {
        uint halfPing = ping / 2;
        return halfPing < MaxNetworkSafetyMargin ? halfPing : MaxNetworkSafetyMargin;
    }

    public static bool IsOnCooldown => Time.Ticks < _nextActionTime;
    public static void BeginCooldown(bool includePing = false) =>
        BeginCooldown(includePing ? QueuedCooldownDuration : CooldownDuration);

    public static void BeginCooldown(long duration) =>
        _nextActionTime = Time.Ticks + (duration > 0 ? duration : 0);
}
