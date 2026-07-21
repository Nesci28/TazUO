using ClassicUO.Configuration;

namespace ClassicUO.Game.Managers;

public static class GlobalActionCooldown
{
    public const uint MaxNetworkSafetyMargin = 50;

    private static long _nextActionTime = 0;
    private static long _cooldownDuration => ProfileManager.CurrentProfile?.MoveMultiObjectDelay ?? 1000;
    public static long CooldownDuration => _cooldownDuration;

    public static uint GetNetworkSafetyMargin(uint ping)
    {
        uint halfPing = ping / 2;
        return halfPing < MaxNetworkSafetyMargin ? halfPing : MaxNetworkSafetyMargin;
    }

    public static bool IsOnCooldown => Time.Ticks < _nextActionTime;
    public static void BeginCooldown() => BeginCooldown(_cooldownDuration);

    public static void BeginCooldown(long duration) =>
        _nextActionTime = Time.Ticks + (duration > 0 ? duration : 0);
}
