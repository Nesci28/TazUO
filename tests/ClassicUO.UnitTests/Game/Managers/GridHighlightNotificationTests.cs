using System.Collections.Generic;
using System.Text.Json;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps.GridHighLight;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.Game.Managers;

public class GridHighlightNotificationTests
{
    [Fact]
    public void EnablingNotificationCreatesSettingsAndTogglingPreservesCustomization()
    {
        var entry = new GridHighlightSetupEntry();
        var data = new GridHighlightData(entry);
        data.ShowNotification = true;
        entry.Notification.Destination.Should().Be(BackpackNotificationDestination.OnScreen);
        entry.Notification.Announcement = "Found {item}!";
        data.ShowNotification = false;
        data.ShowNotification = true;
        entry.Notification.Announcement.Should().Be("Found {item}!");
        entry.LootOnMatch.Should().BeFalse();
    }

    [Fact]
    public void NotificationSettingsStayWithRuleAfterRenameReorderAndSerialization()
    {
        var entry = new GridHighlightSetupEntry
        {
            Name = "Armor",
            ShowNotification = true,
            Notification = new GridHighlightNotificationSettings
            {
                Destination = BackpackNotificationDestination.Overhead,
                Hue = 1153,
                OnScreenFont = "roboto",
                OnScreenFontSize = 30,
                Announcement = "Found {rule}: {item}"
            }
        };
        var config = new GridHighlightsConfig { Highlights = [entry, new() { Name = "Other" }] };
        new GridHighlightData(entry).Name = "Rare Armor";
        config.Highlights.Reverse();

        string json = JsonSerializer.Serialize(config, GridHighlightsJsonContext.DefaultToUse.GridHighlightsConfig);
        GridHighlightsConfig restored = JsonSerializer.Deserialize(json, GridHighlightsJsonContext.DefaultToUse.GridHighlightsConfig);

        restored.Highlights[0].Name.Should().Be("Other");
        restored.Highlights[0].Notification.Should().BeNull();
        restored.Highlights[1].Name.Should().Be("Rare Armor");
        restored.Highlights[1].ShowNotification.Should().BeTrue();
        restored.Highlights[1].Notification.Should().BeEquivalentTo(entry.Notification);
        json.Should().Contain("\"Overhead\"");
    }

    [Fact]
    public void ExistingRulesDoNotGainNotificationsOnLoad()
    {
        List<GridHighlightSetupEntry> rules = JsonSerializer.Deserialize(
            "[{\"name\":\"Existing\",\"loot_on_match\":true}]",
            GridHighlightsJsonContext.DefaultToUse.ListGridHighlightSetupEntry);

        rules[0].ShowNotification.Should().BeFalse();
        rules[0].Notification.Should().BeNull();
        rules[0].LootOnMatch.Should().BeTrue();
    }

    [Fact]
    public void RefreshRenameAndAppearanceChangesDoNotRepeatNotification()
    {
        var tracker = new GridHighlightNotificationTracker();
        Item item = CreateItem();
        var entry = new GridHighlightSetupEntry { Name = "Armor", ShowNotification = true };

        tracker.TryAnnounce(item, entry).Should().BeTrue();
        entry.Name = "Rare Armor";
        entry.Notification = new() { Hue = 1153 };
        tracker.TryAnnounce(item, new GridHighlightData(entry).Entry).Should().BeFalse();
    }

    [Fact]
    public void TwoMatchingRulesWithSameNameNotifyIndependently()
    {
        var tracker = new GridHighlightNotificationTracker();
        Item item = CreateItem();
        var first = new GridHighlightSetupEntry { Name = "Armor", ShowNotification = true };
        var second = new GridHighlightSetupEntry { Name = "Armor", ShowNotification = true };

        tracker.TryAnnounce(item, first).Should().BeTrue();
        tracker.TryAnnounce(item, second).Should().BeTrue();
        tracker.TryAnnounce(item, first).Should().BeFalse();
        tracker.TryAnnounce(item, second).Should().BeFalse();
    }

    [Fact]
    public void DisabledRulesDoNotConsumeFirstNotification()
    {
        var tracker = new GridHighlightNotificationTracker();
        Item item = CreateItem();
        var entry = new GridHighlightSetupEntry { Enabled = false, ShowNotification = true };

        tracker.TryAnnounce(item, entry).Should().BeFalse();
        entry.Enabled = true;
        entry.ShowNotification = false;
        tracker.TryAnnounce(item, entry).Should().BeFalse();
        entry.ShowNotification = true;
        tracker.TryAnnounce(item, entry).Should().BeTrue();
    }

    [Fact]
    public void NewItemWithReusedSerialAndNewSessionCanNotify()
    {
        var tracker = new GridHighlightNotificationTracker();
        Item first = CreateItem();
        Item second = CreateItem();
        first.Serial = second.Serial = 0x40000001;
        var entry = new GridHighlightSetupEntry { ShowNotification = true };

        tracker.TryAnnounce(first, entry).Should().BeTrue();
        tracker.TryAnnounce(second, entry).Should().BeTrue();
        tracker.TryAnnounce(first, entry).Should().BeFalse();
        tracker.Clear();
        tracker.TryAnnounce(first, entry).Should().BeTrue();
    }

    [Theory]
    [InlineData("{RULE}: {ITEM}", "Rare Armor: Plate Tunic")]
    [InlineData("{0}: {1}", "Rare Armor: Plate Tunic")]
    [InlineData("{", "{")]
    [InlineData("", "Rare Armor matched: Plate Tunic")]
    public void AnnouncementUsesCurrentRuleNameAndSharedFormatting(string template, string expected)
    {
        var entry = new GridHighlightSetupEntry
        {
            Name = "Old name",
            Notification = new() { Announcement = template }
        };
        new GridHighlightData(entry).Name = "Rare Armor";

        GridHighlightNotificationManager.FormatAnnouncement(entry, "Plate Tunic").Should().Be(expected);
    }

    private static Item CreateItem()
    {
        Client.UnitTestingActive = true;
        return new Item(new World());
    }
}
