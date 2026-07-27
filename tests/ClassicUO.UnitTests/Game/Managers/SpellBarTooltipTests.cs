using ClassicUO.Game.Managers;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.Game.Managers
{
    public class SpellBarTooltipTests
    {
        [Fact]
        public void FormatSpellTooltip_NameAndDescription_PutsEachOnItsOwnLine()
        {
            CounterBarSlot.FormatSpellTooltip("Clumsy", "Lowers the target's dexterity.")
                .Should().Be("Clumsy\nLowers the target's dexterity.");
        }

        [Theory]
        [InlineData("Clumsy", null, "Clumsy")]
        [InlineData(null, "Lowers the target's dexterity.", "Lowers the target's dexterity.")]
        [InlineData(null, null, "")]
        public void FormatSpellTooltip_MissingValue_UsesAvailableText(string name, string description, string expected)
        {
            CounterBarSlot.FormatSpellTooltip(name, description).Should().Be(expected);
        }
    }
}
