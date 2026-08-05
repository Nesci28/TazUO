using ClassicUO.Game.Managers;
using Xunit;

namespace ClassicUO.UnitTests.Game.Managers;

public sealed class HealthNotificationManagerTests
{
    [Theory]
    [InlineData(30, 100, 30, true)]
    [InlineData(29, 100, 30, true)]
    [InlineData(31, 100, 30, false)]
    [InlineData(1, 3, 30, false)]
    [InlineData(0, 0, 30, false)]
    public void LowHealthThresholdUsesActualRatio(int hits, int maxHits, int threshold, bool expected)
    {
        Assert.Equal(expected, HealthNotificationManager.IsLowHealth(hits, maxHits, threshold));
    }

    [Fact]
    public void LowHealthMessageReplacesAllSupportedTokens()
    {
        string message = HealthNotificationManager.FormatLowHealth(
            "Low health: {health}% ({hits}/{maxhits})",
            25,
            20,
            80
        );

        Assert.Equal("Low health: 25% (20/80)", message);
    }

    [Fact]
    public void DebuffMessageReplacesDebuffTokenCaseInsensitively()
    {
        Assert.Equal(
            "Warning: Mortal wound",
            HealthNotificationManager.FormatDebuff("Warning: {DEBUFF}", "Mortal wound")
        );
    }
}
