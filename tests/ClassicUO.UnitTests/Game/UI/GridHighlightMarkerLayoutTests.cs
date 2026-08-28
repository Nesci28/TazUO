using System.Linq;
using ClassicUO.Game.UI.Controls;
using FluentAssertions;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.Game.UI;

public class GridHighlightMarkerLayoutTests
{
    [Fact]
    public void AdditionalRuleMarkersStartAtTopLeftAndStayInsideTheItem()
    {
        var itemBounds = new Rectangle(10, 20, 40, 40);

        Rectangle[] markers = GridItem.GetAdditionalHighlightMarkerBounds(itemBounds, 20);

        markers.Should().HaveCount(20);
        markers[0].X.Should().Be(itemBounds.X + 2);
        markers[0].Y.Should().Be(itemBounds.Y + 2);
        markers.Distinct().Should().HaveCount(20);
        markers.Should().OnlyContain(marker =>
            marker.Left >= itemBounds.Left &&
            marker.Top >= itemBounds.Top &&
            marker.Right <= itemBounds.Right &&
            marker.Bottom <= itemBounds.Bottom);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NoAdditionalRulesProduceNoMarkers(int markerCount)
    {
        GridItem.GetAdditionalHighlightMarkerBounds(new Rectangle(0, 0, 40, 40), markerCount)
            .Should().BeEmpty();
    }
}
