using ClassicUO.Game.Data;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.Game.Data
{
    public class SpellBookDefinitionTests
    {
        [Theory]
        [InlineData(1, 1061290)]
        [InlineData(64, 1061353)]
        [InlineData(101, 1061390)]
        [InlineData(117, 1061406)]
        [InlineData(201, 1061490)]
        [InlineData(210, 1061499)]
        [InlineData(401, 1063263)]
        [InlineData(406, 1063268)]
        [InlineData(501, 1063279)]
        [InlineData(508, 1063286)]
        [InlineData(601, 1072042)]
        [InlineData(616, 1072057)]
        [InlineData(678, 1095193)]
        [InlineData(693, 1095208)]
        [InlineData(701, 1115689)]
        [InlineData(706, 1115694)]
        [InlineData(707, 1155938)]
        [InlineData(745, 1155976)]
        public void GetSpellDescriptionCliloc_KnownSpell_ReturnsSpellbookDescription(int spellID, int expectedCliloc)
        {
            SpellBookDefinition.GetSpellDescriptionCliloc(spellID).Should().Be(expectedCliloc);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(65)]
        [InlineData(100)]
        [InlineData(746)]
        public void GetSpellDescriptionCliloc_UnknownSpell_ReturnsZero(int spellID)
        {
            SpellBookDefinition.GetSpellDescriptionCliloc(spellID).Should().Be(0);
        }
    }
}
