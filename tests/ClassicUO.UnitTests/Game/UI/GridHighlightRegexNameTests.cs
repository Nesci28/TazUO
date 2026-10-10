using System.Collections.Generic;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps.GridHighLight;
using ClassicUO.UnitTests.Fixtures;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.Game.UI;

[Collection(CurrentProfileCollection.Name)]
public class GridHighlightRegexNameTests
{
    [Theory]
    [InlineData("$ring|bracelet", "a ring", true)]
    [InlineData("$ring|bracelet", "a bracelet", true)]
    [InlineData("$ring|bracelet", "a sword", false)]
    [InlineData("$^ring$", "ring", true)]
    [InlineData("$^ring$", "a ring", false)]
    [InlineData("$^Ring$", "Ring", true)]
    [InlineData("$^Ring$", "ring", false)]
    [InlineData("$(?i)^ring$", "RING", true)]
    [InlineData("$^\\d+ gold coins$", "100 gold coins", true)]
    [InlineData("$^\\d+ gold coins$", "gold coins", false)]
    [InlineData("$^\\S+ ring$", "enchanted ring", true)]
    [InlineData("$^\\S+ ring$", "an enchanted ring", false)]
    [InlineData("  $^ring$  ", "ring", true)]
    [InlineData("ring", "RING", true)]
    [InlineData("ring", "a ring", false)]
    [InlineData("gold coins", "100 gold coins", true)]
    [InlineData("ring (gold)", "ring (gold)", true)]
    [InlineData("ring|bracelet", "ring", false)]
    [InlineData("$[", "ring", false)]
    [InlineData("$", "ring", false)]
    public void ItemNamesSupportRegexAndPreserveLiteralMatching(string pattern, string itemName, bool expected)
    {
        foreach (bool acceptExtraProperties in new[] { true, false })
        {
            GridHighlightData rule = CreateRule([pattern], acceptExtraProperties);

            rule.IsMatch(new ItemPropertiesData(itemName + "\nLuck 100")).Should().Be(expected);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InvalidRegexDoesNotPreventOtherNameEntriesFromMatching(bool acceptExtraProperties)
    {
        GridHighlightData rule = CreateRule(["$[", "bracelet", "$ring"], acceptExtraProperties);

        rule.IsMatch(new ItemPropertiesData("bracelet\nLuck 100")).Should().BeTrue();
        rule.IsMatch(new ItemPropertiesData("a ring\nLuck 100")).Should().BeTrue();
        rule.IsMatch(new ItemPropertiesData("sword\nLuck 100")).Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RegexMatchesOnlyTheNameAndStillRequiresConfiguredProperties(bool acceptExtraProperties)
    {
        GridHighlightData rule = CreateRule(["$ring"], acceptExtraProperties);

        rule.IsMatch(new ItemPropertiesData("sword\nLuck 100\nring")).Should().BeFalse();
        rule.IsMatch(new ItemPropertiesData("ring\nLuck 99")).Should().BeFalse();
        rule.IsMatch(new ItemPropertiesData("ring\nLuck 100")).Should().BeTrue();
    }

    private static GridHighlightData CreateRule(List<string> itemNames, bool acceptExtraProperties) =>
        new(new GridHighlightSetupEntry
        {
            ItemNames = itemNames,
            AcceptExtraProperties = acceptExtraProperties,
            Properties = [new GridHighlightProperty { Name = "Luck", MinValue = 100 }]
        });
}
