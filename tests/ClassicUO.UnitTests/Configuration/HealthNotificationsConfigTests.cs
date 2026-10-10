using System.Text.Json;
using ClassicUO.Configuration;
using Xunit;

namespace ClassicUO.UnitTests.Configuration;

public sealed class HealthNotificationsConfigTests
{
    [Fact]
    public void LegacyHueMigratesToBothNotificationTypes()
    {
        const string json = """
            {
              "hue": 87
            }
            """;

        HealthNotificationsConfig config = JsonSerializer.Deserialize(
            json,
            HealthNotificationsJsonContext.DefaultToUse.HealthNotificationsConfig
        );

        Assert.True(config.MigrateLegacyHue());
        Assert.Equal((ushort)87, config.LowHealthHue);
        Assert.Equal((ushort)87, config.DebuffHue);
        Assert.Null(config.LegacyHue);

        string migrated = JsonSerializer.Serialize(
            config,
            HealthNotificationsJsonContext.DefaultToUse.HealthNotificationsConfig
        );

        Assert.Contains("\"low_health_hue\": 87", migrated);
        Assert.Contains("\"debuff_hue\": 87", migrated);
        Assert.DoesNotContain("\n  \"hue\":", migrated);
    }

    [Fact]
    public void NotificationHuesRoundTripIndependently()
    {
        var config = new HealthNotificationsConfig
        {
            LowHealthHue = 32,
            DebuffHue = 63
        };

        string json = JsonSerializer.Serialize(
            config,
            HealthNotificationsJsonContext.DefaultToUse.HealthNotificationsConfig
        );
        HealthNotificationsConfig restored = JsonSerializer.Deserialize(
            json,
            HealthNotificationsJsonContext.DefaultToUse.HealthNotificationsConfig
        );

        Assert.Equal((ushort)32, restored.LowHealthHue);
        Assert.Equal((ushort)63, restored.DebuffHue);
    }
}
