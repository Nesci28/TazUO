using ClassicUO.Game.Data;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.Game.Data;

public class WorldMapCoordinatesTests
{
    [Theory]
    [InlineData(200, 200, 1f, false, 1000, 2000)]
    [InlineData(200, 200, 1f, true, 1000, 2000)]
    [InlineData(220, 180, 1f, false, 1020, 1980)]
    [InlineData(220, 180, 2f, false, 1010, 1990)]
    [InlineData(220, 180, 0.5f, false, 1040, 1960)]
    [InlineData(341, 341, 1f, true, 1200, 2000)]
    [InlineData(341, 341, 2f, true, 1100, 2000)]
    public void MouseCoordinatesRespectDisplayedCenterZoomAndRotation(
        int x, int y, float zoom, bool flipped, int expectedX, int expectedY)
    {
        Point coordinates = WorldMapCoordinates.CanvasToWorld(new Point(x, y), new Point(400, 400),
            new Point(1000, 2000), zoom, flipped);

        Assert.Equal(new Point(expectedX, expectedY), coordinates);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResizedMapCenterRemainsTheDisplayedWorldPosition(bool flipped)
    {
        var center = new Point(2500, 1750);
        Point coordinates = WorldMapCoordinates.CanvasToWorld(new Point(300, 150), new Point(600, 300),
            center, 1f, flipped);

        Assert.Equal(center, coordinates);
    }
}
