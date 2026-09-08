using ClassicUO.Game.UI.Controls;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.Game.UI;

public class WorldMapControlsLayoutTests
{
    [Theory]
    [InlineData(100, 100, 16, 16)]
    [InlineData(100, 100, 32, 32)]
    [InlineData(120, 100, 32, 32)]
    [InlineData(400, 400, 16, 16)]
    [InlineData(1920, 1080, 32, 32)]
    public void ControlsLeaveLockAndResizeHotspotsClearAtEveryMapSize(int width, int height, int lockWidth, int lockHeight)
    {
        var resizeHandle = new Rectangle(width - 16, height - 16, 16, 16);
        var (actions, zoom, pathfind) = WorldMapControlsLayout.Calculate(width, 6,
            new Point(lockWidth, lockHeight), resizeHandle);
        // ResizableGump accepts mouse-up on the right and bottom edges as well.
        var lockHotspot = new Rectangle(0, 0, lockWidth + 1, lockHeight + 1);
        var mapInterior = new Rectangle(4, 4, width - 8, height - 8);

        Assert.False(lockHotspot.Intersects(actions));
        Assert.False(lockHotspot.Intersects(zoom));
        Assert.False(lockHotspot.Intersects(pathfind));
        Assert.False(actions.Intersects(zoom));
        Assert.False(actions.Intersects(pathfind));
        Assert.False(zoom.Intersects(pathfind));
        Assert.False(resizeHandle.Intersects(actions));
        Assert.False(resizeHandle.Intersects(zoom));
        Assert.False(resizeHandle.Intersects(pathfind));
        Assert.True(mapInterior.Contains(actions));
        Assert.True(mapInterior.Contains(zoom));
        Assert.True(mapInterior.Contains(pathfind));
        Assert.True(actions.Width > actions.Height);
        Assert.True(zoom.Height > zoom.Width);
        Assert.True(actions.Right < zoom.Left);
        Assert.Equal(width - 6, zoom.Right);
        Assert.Equal(6, zoom.Top);
        Assert.True(pathfind.Top > zoom.Bottom);
        if (height > 100)
            Assert.Equal(zoom.Left, pathfind.Left);
    }
}
