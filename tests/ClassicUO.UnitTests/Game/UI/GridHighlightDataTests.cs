using System.Collections.Generic;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps.GridHighLight;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.Game.UI;

public class GridHighlightDataTests
{
    [Fact]
    public void EmptyRuleDoesNotMatchEverything()
    {
        GridHighlightData rule = CreateRule();

        rule.HasSelectionCriteria().Should().BeFalse();
        rule.IsMatch(Tooltip("a ring\nLuck 100")).Should().BeFalse();
    }

    [Fact]
    public void ItemNameIgnoresStackAmountButPreservesNamesStartingWithDigits()
    {
        GridHighlightData coins = CreateRule(itemNames: ["gold coins"]);
        GridHighlightData anniversary = CreateRule(itemNames: ["10th Anniversary Sculpture"]);

        coins.IsMatch(Tooltip("10 gold coins")).Should().BeTrue();
        anniversary.IsMatch(Tooltip("10th Anniversary Sculpture")).Should().BeTrue();
        CreateRule(itemNames: ["15 Year Anniversary Sculpture"])
            .IsMatch(Tooltip("10 Year Anniversary Sculpture"))
            .Should().BeFalse();
    }

    [Fact]
    public void NameNormalizationRemovesRealHtmlWithoutDiscardingComparisonText()
    {
        CreateRule(itemNames: ["ring"])
            .IsMatch(Tooltip("<basefont color=red>ring</basefont>"))
            .Should().BeTrue();
        CreateRule(itemNames: ["ring"])
            .IsMatch(Tooltip("/c[#FF0000]ring/cd"))
            .Should().BeTrue();
        CreateRule(itemNames: ["gold coins"])
            .IsMatch(Tooltip("gold   coins"))
            .Should().BeTrue();
        CreateRule(itemNames: ["quality < 5"])
            .IsMatch(Tooltip("quality < 5"))
            .Should().BeTrue();
    }

    [Fact]
    public void RequiredAndOptionalPropertiesUseTheSameMinimumValueRules()
    {
        GridHighlightData rule = CreateRule(properties:
        [
            new GridHighlightProperty { Name = "Luck", MinValue = 100 },
            new GridHighlightProperty { Name = "Damage Increase", MinValue = 20, IsOptional = true }
        ]);

        rule.IsMatch(Tooltip("ring\nLuck 99")).Should().BeFalse();
        rule.IsMatch(Tooltip("ring\nLuck 100\nDamage Increase 19%")).Should().BeTrue();
    }

    [Fact]
    public void OptionalPropertyBelowMinimumDoesNotIncreaseMatchingCount()
    {
        GridHighlightData rule = CreateRule(properties:
        [
            new GridHighlightProperty { Name = "Damage Increase", MinValue = 20, IsOptional = true }
        ]);
        rule.MinimumMatchingProperty = 1;

        rule.IsMatch(Tooltip("ring\nDamage Increase 19%")).Should().BeFalse();
        rule.IsMatch(Tooltip("ring\nDamage Increase 20%")).Should().BeTrue();
    }

    [Fact]
    public void StrictModeRejectsExtraPropertiesWhileFlexibleModeAcceptsThem()
    {
        GridHighlightData rule = CreateRule(properties:
        [
            new GridHighlightProperty { Name = "Luck", MinValue = 100 }
        ]);
        ItemPropertiesData item = Tooltip("ring\nLuck 100\nDamage Increase 20%");

        rule.AcceptExtraProperties = false;
        rule.IsMatch(item).Should().BeFalse();

        rule.AcceptExtraProperties = true;
        rule.IsMatch(item).Should().BeTrue();
    }

    [Fact]
    public void PropertyNamesUsePhrasePrefixesWithoutSubstringCollisions()
    {
        GridHighlightData rule = CreateRule(properties:
        [
            new GridHighlightProperty { Name = "Damage Increase", MinValue = 10 }
        ]);

        rule.IsMatch(Tooltip("ring\nSpell Damage Increase 20%")).Should().BeFalse();
        rule.IsMatch(Tooltip("ring\nDamage Increase 20%")).Should().BeTrue();
    }

    [Fact]
    public void EnglishClilocNameLetsDefaultRulesMatchLocalizedTooltips()
    {
        GridHighlightData rule = CreateRule(properties:
        [
            new GridHighlightProperty { Name = "Luck", MinValue = 100 }
        ]);
        ItemPropertiesData localized = Tooltip("anneau\nChance 100");
        localized.singlePropertyData[0].EnglishName = "Luck ~1_val~";

        rule.IsMatch(localized).Should().BeTrue();
    }

    [Fact]
    public void ExclusionsRespectWordBoundaries()
    {
        GridHighlightData rule = CreateRule(exclusions: ["Luck"]);

        rule.IsMatch(Tooltip("ring\nUnlucky 100")).Should().BeTrue();
        rule.IsMatch(Tooltip("ring\nLuck 100")).Should().BeFalse();
    }

    [Fact]
    public void CustomRarityMatchesAndIsNotTreatedAsAnExtraProperty()
    {
        GridHighlightData rule = CreateRule(rarities: ["Mythic Relic"]);
        rule.AcceptExtraProperties = false;

        rule.IsMatch(Tooltip("artifact\nMythic Relic")).Should().BeTrue();
        rule.IsMatch(Tooltip("artifact\nLegendary Artifact")).Should().BeFalse();
    }

    [Fact]
    public void WeightFilterRequiresAWeightLineAndParsesLocalizedDecimals()
    {
        GridHighlightData rule = CreateRule(itemNames: ["ore"]);
        rule.Overweight = true;
        rule.MinimumWeight = 2;
        rule.MaximumWeight = 3;

        rule.IsMatch(Tooltip("ore\nPoids: 2,5 pierres")).Should().BeTrue();
        rule.IsMatch(Tooltip("ore\n重量: 2 石")).Should().BeTrue();
        rule.IsMatch(Tooltip("ore\n무게: 2 stones")).Should().BeTrue();
        rule.IsMatch(Tooltip("ore\nPoids: 3,5 pierres")).Should().BeFalse();
        rule.IsMatch(Tooltip("ore\nPhysical Resist 10%")).Should().BeFalse();

        GridHighlightData groupedWeightRule = CreateRule(itemNames: ["ore"]);
        groupedWeightRule.Overweight = true;
        groupedWeightRule.MinimumWeight = 999;
        groupedWeightRule.MaximumWeight = 1001;
        groupedWeightRule.IsMatch(Tooltip("ore\nWeight: 1,000 stones")).Should().BeTrue();
    }

    [Fact]
    public void OtherSlotIsIndependentFromNamedEquipmentSlots()
    {
        GridHighlightData rule = CreateRule();
        rule.EquipmentSlots.Other = true;
        rule.EquipmentSlots.Ring = false;

        rule.HasSelectionCriteria().Should().BeTrue();
        rule.MatchesSlot((byte)Layer.Invalid).Should().BeTrue();
        rule.MatchesSlot((byte)Layer.Ring).Should().BeFalse();
        rule.MatchesSlot((byte)Layer.Backpack).Should().BeFalse();
    }

    [Fact]
    public void DuplicatePropertyNamesAreMergedCaseInsensitivelyUsingStrictestRule()
    {
        GridHighlightData rule = CreateRule(properties:
        [
            new GridHighlightProperty { Name = "luck", MinValue = 50, IsOptional = true },
            new GridHighlightProperty { Name = "Luck", MinValue = 80 }
        ]);

        rule.IsMatch(Tooltip("ring\nLuck 79")).Should().BeFalse();
        rule.IsMatch(Tooltip("ring\nLuck 80")).Should().BeTrue();
    }

    private static GridHighlightData CreateRule(
        List<string> itemNames = null,
        List<GridHighlightProperty> properties = null,
        List<string> exclusions = null,
        List<string> rarities = null) =>
        new(new GridHighlightSetupEntry
        {
            ItemNames = itemNames ?? [],
            Properties = properties ?? [],
            ExcludeNegatives = exclusions ?? [],
            RequiredRarities = rarities ?? []
        });

    private static ItemPropertiesData Tooltip(string value) => new(value);
}
