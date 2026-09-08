using System;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.UnitTests.Game.LegionScript;
using Xunit;

namespace ClassicUO.UnitTests.Game.UI;

[Collection(MainThreadCollection.Name)]
public class WorldMapControlButtonTests
{
    private sealed class MapInputCounter : Area
    {
        internal int Events;
        public override void OnMouseDown(int x, int y, MouseButtonType button) => Events++;
        public override void OnMouseUp(int x, int y, MouseButtonType button) => Events++;
        public override bool OnMouseDoubleClick(int x, int y, MouseButtonType button)
        {
            Events++;
            return true;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClickingControlsDoesNotPanTargetOrDoubleClickTheMap(bool pathfinding) => OnMainThread(() =>
    {
        var map = new MapInputCounter();
        int clicks = 0;
        var button = new WorldMapControlButton(pathfinding ? WorldMapControlIcon.Pathfind : WorldMapControlIcon.FreeView,
            () => clicks++);
        map.Add(button);

        button.OnMouseDown(10, 10, MouseButtonType.Left);
        button.OnMouseUp(10, 10, MouseButtonType.Left);
        Assert.True(button.OnMouseDoubleClick(10, 10, MouseButtonType.Left));
        button.OnMouseDown(10, 10, MouseButtonType.Middle);
        button.OnMouseUp(10, 10, MouseButtonType.Middle);

        Assert.Equal(1, clicks);
        Assert.Equal(0, map.Events);
        Assert.False(button.CanMove);
        Assert.True(Mouse.CancelDoubleClick);
    });

    [Fact]
    public void ZoomLimitStillInterceptsClicksWithoutActivating() => OnMainThread(() =>
    {
        var map = new MapInputCounter();
        int clicks = 0;
        var button = new WorldMapControlButton(WorldMapControlIcon.ZoomIn, () => clicks++) { CanActivate = false };
        map.Add(button);
        button.OnMouseDown(10, 10, MouseButtonType.Left);
        button.OnMouseUp(10, 10, MouseButtonType.Left);

        Assert.True(button.AcceptMouseInput);
        Assert.Equal(0, clicks);
        Assert.Equal(0, map.Events);
    });

    [Fact]
    public void ReleasingOutsideCancelsAndEachSubsequentClickFiresOnce() => OnMainThread(() =>
    {
        int clicks = 0;
        var button = new WorldMapControlButton(WorldMapControlIcon.ZoomOut, () => clicks++);
        button.OnMouseDown(10, 10, MouseButtonType.Left);
        button.OnMouseUp(30, 10, MouseButtonType.Left);
        Assert.Equal(0, clicks);

        for (int i = 0; i < 2; i++)
        {
            button.OnMouseDown(10, 10, MouseButtonType.Left);
            button.OnMouseUp(10, 10, MouseButtonType.Left);
            button.OnMouseUp(10, 10, MouseButtonType.Left);
        }
        Assert.Equal(2, clicks);
    });

    private static void OnMainThread(Action test) => MainThreadQueue.BubblingInvokeOnMainThread(() =>
    {
        bool cancelDoubleClick = Mouse.CancelDoubleClick;
        try { test(); }
        finally { Mouse.CancelDoubleClick = cancelDoubleClick; }
    });
}
