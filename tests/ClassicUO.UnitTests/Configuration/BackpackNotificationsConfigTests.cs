using System.Text.Json;
using ClassicUO.Configuration;
using Xunit;

namespace ClassicUO.UnitTests.Configuration;

public sealed class BackpackNotificationsConfigTests
{
    [Fact]
    public void LegacyDestinationsMigrateToSingleOnScreenDestination()
    {
        const string json =
            """
            {
              "rules": [
                {
                  "journal": true,
                  "journal_hue": 63,
                  "overhead": false,
                  "overhead_hue": 64,
                  "on_screen": true,
                  "on_screen_hue": 87
                }
              ]
            }
            """;

        BackpackNotificationsConfig config = JsonSerializer.Deserialize(
            json,
            BackpackNotificationsJsonContext.DefaultToUse.BackpackNotificationsConfig
        );
        BackpackNotificationConfigEntry rule = Assert.Single(config.Rules);

        Assert.True(rule.MigrateLegacyDestination());
        Assert.Equal(BackpackNotificationDestination.OnScreen, rule.Destination);
        Assert.Equal((ushort)87, rule.Hue);

        string migrated = JsonSerializer.Serialize(
            config,
            BackpackNotificationsJsonContext.DefaultToUse.BackpackNotificationsConfig
        );
        Assert.Contains("\"destination\": \"OnScreen\"", migrated);
        Assert.DoesNotContain("\"journal\"", migrated);
        Assert.DoesNotContain("\"overhead\"", migrated);
        Assert.DoesNotContain("\"on_screen\"", migrated);
    }
}
