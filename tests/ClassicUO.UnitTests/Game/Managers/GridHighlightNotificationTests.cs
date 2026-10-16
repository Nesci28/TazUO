using System.Collections.Generic;
using System.Text.Json;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.Data;
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

        tracker.ObserveItem(item, [entry]);
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

        tracker.ObserveItem(item, [first, second]);
        tracker.TryAnnounce(item, first).Should().BeTrue();
        tracker.TryAnnounce(item, second).Should().BeTrue();
        tracker.TryAnnounce(item, first).Should().BeFalse();
        tracker.TryAnnounce(item, second).Should().BeFalse();
    }

    [Fact]
    public void EnablingNotificationDoesNotAnnouncePreviouslyDiscoveredLoot()
    {
        var tracker = new GridHighlightNotificationTracker();
        Item item = CreateItem();
        var entry = new GridHighlightSetupEntry { Enabled = false, ShowNotification = true };

        tracker.ObserveItem(item, [entry]);
        tracker.TryAnnounce(item, entry).Should().BeFalse();
        entry.Enabled = true;
        entry.ShowNotification = false;
        tracker.TryAnnounce(item, entry).Should().BeFalse();
        entry.ShowNotification = true;
        tracker.ObserveItem(item, [entry]);
        tracker.TryAnnounce(item, entry).Should().BeFalse();

        Item newLoot = CreateContainedItem(item.World, 0x40000002, item.Container);
        tracker.ObserveItem(newLoot, [entry]);
        tracker.TryAnnounce(newLoot, entry).Should().BeTrue();
    }

    [Fact]
    public void ReopeningCorpseDoesNotAnnounceReplacementItemWithSameSerial()
    {
        var tracker = new GridHighlightNotificationTracker();
        Item first = CreateItem();
        var entry = new GridHighlightSetupEntry { ShowNotification = true };

        tracker.ObserveItem(first, [entry]);
        tracker.TryAnnounce(first, entry).Should().BeTrue();
        tracker.CompleteItem(first.Serial);

        Item second = CreateContainedItem(first.World, first.Serial, first.Container);
        tracker.ObserveItem(second, [entry]);
        tracker.TryAnnounce(second, entry).Should().BeFalse();
        tracker.TryAnnounce(first, entry).Should().BeFalse();
    }

    [Fact]
    public void NewSessionCanAnnounceCorpseLootAgain()
    {
        var tracker = new GridHighlightNotificationTracker();
        Item item = CreateItem();
        var entry = new GridHighlightSetupEntry { ShowNotification = true };

        tracker.ObserveItem(item, [entry]);
        tracker.TryAnnounce(item, entry).Should().BeTrue();
        tracker.Clear();
        tracker.ObserveItem(item, [entry]);
        tracker.TryAnnounce(item, entry).Should().BeTrue();
    }

    [Theory]
    [InlineData(0x0E75, 0x00000001u)] // Backpack
    [InlineData(0x09AB, 0x00000001u)] // Bank
    [InlineData(0x0E40, 0xFFFFFFFFu)] // World storage chest
    public void BrowsingInventoryAndStorageDoesNotNotify(ushort graphic, uint owner)
    {
        var tracker = new GridHighlightNotificationTracker();
        Item item = CreateItem(graphic);
        item.World.Items[item.Container].Container = owner;
        var entry = new GridHighlightSetupEntry { ShowNotification = true };

        tracker.ObserveItem(item, [entry]);
        tracker.TryAnnounce(item, entry).Should().BeFalse();
    }

    [Fact]
    public void LootInNestedCorpseBagCanNotify()
    {
        var tracker = new GridHighlightNotificationTracker();
        Item item = CreateItem();
        Item bag = CreateContainedItem(item.World, 0x40000011, item.Container);
        item.Container = bag.Serial;
        var entry = new GridHighlightSetupEntry { ShowNotification = true };

        tracker.ObserveItem(item, [entry]);
        tracker.TryAnnounce(item, entry).Should().BeTrue();
    }

    [Fact]
    public void OsiDeadMobileCorpseLootCanNotify()
    {
        var tracker = new GridHighlightNotificationTracker();
        Item item = CreateItem();
        uint corpseSerial = item.Container;
        uint deadMobileSerial = 0x00000002;
        item.World.CorpseManager.Add(corpseSerial, deadMobileSerial, Direction.North, false);
        item.Container = deadMobileSerial;
        var entry = new GridHighlightSetupEntry { ShowNotification = true };

        tracker.ObserveItem(item, [entry]);
        tracker.TryAnnounce(item, entry).Should().BeTrue();
    }

    [Fact]
    public void LootWithIncompleteContainerAncestryWaitsForCorpseContext()
    {
        var tracker = new GridHighlightNotificationTracker();
        Item item = CreateItem();
        Item corpse = item.World.Items[item.Container];
        item.World.Items.Remove(corpse.Serial);
        var entry = new GridHighlightSetupEntry { ShowNotification = true };

        tracker.ObserveItem(item, [entry]);
        tracker.TryAnnounce(item, entry).Should().BeFalse();

        item.World.Items.Add(corpse.Serial, corpse);
        tracker.ObserveItem(item, [entry]);
        tracker.TryAnnounce(item, entry).Should().BeTrue();
    }

    [Fact]
    public void NewLootInPreviouslyOpenedCorpseCanStillNotify()
    {
        var tracker = new GridHighlightNotificationTracker();
        Item first = CreateItem();
        var entry = new GridHighlightSetupEntry { ShowNotification = true };

        tracker.ObserveItem(first, [entry]);
        tracker.TryAnnounce(first, entry).Should().BeTrue();
        tracker.CompleteItem(first.Serial);

        Item second = CreateContainedItem(first.World, 0x40000002, first.Container);
        tracker.ObserveItem(second, [entry]);
        tracker.TryAnnounce(second, entry).Should().BeTrue();
    }

    [Fact]
    public void LootTransferWhileWaitingForPropertiesPreservesOriginalDiscovery()
    {
        var tracker = new GridHighlightNotificationTracker();
        Item item = CreateItem();
        var entry = new GridHighlightSetupEntry { ShowNotification = true };
        tracker.ObserveItem(item, [entry]);

        Item bag = CreateContainedItem(item.World, 0x40000012, 0x00000001);
        Item lootedItem = CreateContainedItem(item.World, item.Serial, bag.Serial);
        tracker.ObserveItem(lootedItem, [entry]);
        tracker.TryAnnounce(lootedItem, entry).Should().BeTrue();
        tracker.CompleteItem(lootedItem.Serial);

        Item storedItem = CreateContainedItem(item.World, item.Serial, bag.Serial);
        tracker.ObserveItem(storedItem, [entry]);
        tracker.TryAnnounce(storedItem, entry).Should().BeFalse();
    }

    [Fact]
    public void PreviouslyOwnedItemDoesNotBecomeNewLootWhenMovedIntoCorpse()
    {
        var tracker = new GridHighlightNotificationTracker();
        Item item = CreateItem(0x0E75);
        var entry = new GridHighlightSetupEntry { ShowNotification = true };
        tracker.ObserveItem(item, [entry]);

        Item corpse = CreateContainedItem(item.World, 0x40000020, 0xFFFFFFFF);
        corpse.Graphic = 0x2006;
        Item movedItem = CreateContainedItem(item.World, item.Serial, corpse.Serial);
        tracker.ObserveItem(movedItem, [entry]);
        tracker.TryAnnounce(movedItem, entry).Should().BeFalse();
    }

    [Fact]
    public void RecheckingNonmatchingLootDoesNotCreateNewDiscovery()
    {
        var tracker = new GridHighlightNotificationTracker();
        Item item = CreateItem();
        var entry = new GridHighlightSetupEntry { ShowNotification = true };

        tracker.ObserveItem(item, [entry]);
        tracker.CompleteItem(item.Serial); // Properties were evaluated without a match.
        tracker.ObserveItem(item, [entry]);
        tracker.TryAnnounce(item, entry).Should().BeFalse();
    }

    [Fact]
    public void RuleRefreshSilencesPendingAndExistingLootButAllowsLaterDiscoveries()
    {
        var tracker = new GridHighlightNotificationTracker();
        Item pending = CreateItem();
        var entry = new GridHighlightSetupEntry { ShowNotification = true };
        tracker.ObserveItem(pending, [entry]);
        tracker.SuppressPendingNotifications();
        tracker.TryAnnounce(pending, entry).Should().BeFalse();
        tracker.ObserveItem(pending, [entry]);
        tracker.TryAnnounce(pending, entry).Should().BeFalse();

        Item existing = CreateContainedItem(pending.World, 0x40000002, pending.Container);
        tracker.ObserveItem(existing, [entry], allowNotification: false);
        tracker.ObserveItem(existing, [entry]);
        tracker.TryAnnounce(existing, entry).Should().BeFalse();

        Item newLoot = CreateContainedItem(pending.World, 0x40000003, pending.Container);
        tracker.ObserveItem(newLoot, [entry]);
        tracker.TryAnnounce(newLoot, entry).Should().BeTrue();
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

    private static Item CreateItem(ushort rootGraphic = 0x2006)
    {
        Client.UnitTestingActive = true;
        var world = new World();
        var root = new Item(world) { Serial = 0x40000010, Graphic = rootGraphic };
        world.Items.Add(root.Serial, root);
        return CreateContainedItem(world, 0x40000001, root.Serial);
    }

    private static Item CreateContainedItem(World world, uint serial, uint container)
    {
        var item = new Item(world) { Serial = serial, Container = container };
        world.Items[serial] = item;
        return item;
    }
}
