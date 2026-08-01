// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.Game.UI;

public class ItemComparisonStatChangesTests
{
    [Fact]
    public void ShowsChangedGainedAndLostStats()
    {
        ItemPropertiesData candidate = Tooltip(
            "Candidate Ring\nFire Resist 15%\nLuck 100\nMana Increase 8"
        );
        ItemPropertiesData equipped = Tooltip(
            "Equipped Ring\nFire Resist 10%\nLuck 120\nHit Point Increase 5"
        );

        string result = ItemComparisonStatChanges.BuildSection(candidate, equipped);

        result.Should().Contain("Stat Changes if Equipped");
        result.Should().Contain("/c[green]+5%/cd Fire Resist");
        result.Should().Contain("/c[red]-20/cd Luck");
        result.Should().Contain("/c[green]+8/cd Mana Increase");
        result.Should().Contain("/c[red]-5/cd Hit Point Increase");
    }

    [Fact]
    public void OmitsUnchangedAndItemMetadataValues()
    {
        ItemPropertiesData candidate = Tooltip(
            "Candidate Sword\nDamage Increase 20%\nDurability 40 / 50\nWeight 7 stones\nArtifact Rarity 12"
        );
        ItemPropertiesData equipped = Tooltip(
            "Equipped Sword\nDamage Increase 20%\nDurability 10 / 50\nWeight 5 stones\nArtifact Rarity 8"
        );

        string result = ItemComparisonStatChanges.BuildSection(candidate, equipped);

        result.Should().Contain("No stat changes");
        result.Should().NotContain("Damage Increase");
        result.Should().NotContain("Durability");
        result.Should().NotContain("Weight");
        result.Should().NotContain("Artifact Rarity");
    }

    [Fact]
    public void ShowsBothEndsOfAChangedRange()
    {
        ItemPropertiesData candidate = Tooltip("Candidate Sword\nWeapon Damage 12 - 18");
        ItemPropertiesData equipped = Tooltip("Equipped Sword\nWeapon Damage 10 - 20");

        string result = ItemComparisonStatChanges.BuildSection(candidate, equipped);

        result.Should().Contain("/c[green]+2/cd / /c[red]-2/cd Weapon Damage");
    }

    [Fact]
    public void MatchesNamesWithoutCaseSensitivity()
    {
        ItemPropertiesData candidate = Tooltip("Candidate Ring\nLuck 125");
        ItemPropertiesData equipped = Tooltip("Equipped Ring\nluck 100");
        string result = ItemComparisonStatChanges.BuildSection(candidate, equipped);

        result.Should().Contain("/c[green]+25/cd Luck");
        result.Should().NotContain("/c[green]+125/cd");
        result.Should().NotContain("/c[red]-100/cd");
    }

    private static ItemPropertiesData Tooltip(string value) => new(value);
}
