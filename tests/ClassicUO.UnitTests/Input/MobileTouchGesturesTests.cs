using System.Collections.Generic;
using ClassicUO.Input;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.Input;

public class MobileTouchGesturesTests
{
    [Fact]
    public void FingerJitterRemainsATapAtTheOriginalPosition()
    {
        var gestures = new MobileTouchGestures();
        var moves = new List<Vector2>();
        Vector2 released = default;
        gestures.PointerMove += moves.Add;
        gestures.PointerUp += p => released = p;
        gestures.Down(1, 1, new(100, 100), true);
        gestures.Move(1, 1, new(103, 104));
        gestures.Up(1, 1, new(104, 102));
        Assert.Empty(moves);
        Assert.Equal(new Vector2(100, 100), released);
    }

    [Fact]
    public void FastDragDispatchesMovementBeforeReleaseEvenWithoutMotionEvents()
    {
        var gestures = new MobileTouchGestures();
        var events = new List<string>();
        gestures.PointerDown += _ => events.Add("down");
        gestures.PointerMove += _ => events.Add("drag");
        gestures.PointerUp += _ => events.Add("up");
        gestures.Down(1, 1, new(100, 100), true);
        gestures.Up(1, 1, new(200, 150));
        Assert.Equal(new[] { "down", "drag", "up" }, events);
    }

    [Fact]
    public void PinchCancelsThePointerAndNeitherFingerReleaseClicks()
    {
        var gestures = new MobileTouchGestures();
        int canceled = 0, released = 0, pressed = 0;
        var scales = new List<float>();
        gestures.PointerDown += _ => pressed++;
        gestures.PointerCancel += () => canceled++;
        gestures.PointerUp += _ => released++;
        gestures.PinchScale += scales.Add;
        gestures.Down(1, 1, new(100, 100), true);
        gestures.Down(1, 2, new(200, 100), true);
        gestures.Move(1, 2, new(300, 100));
        gestures.Move(1, 2, new(150, 100));
        Assert.Equal(new[] { 2f, 0.5f }, scales);
        gestures.Up(1, 1, new(100, 100));
        gestures.Move(1, 2, new(320, 100));
        gestures.Down(1, 3, new(50, 100), true);
        gestures.Up(1, 2, new(320, 100));
        gestures.Up(1, 3, new(50, 100));
        Assert.Equal(1, canceled);
        Assert.Equal(1, pressed);
        Assert.Equal(0, released);
        Assert.Equal(2, scales.Count);
        gestures.Down(1, 4, new(100, 100), true);
        gestures.Up(1, 4, new(100, 100));
        Assert.Equal(1, released);
    }

    [Fact]
    public void SecondFingerCannotZoomThroughAGump()
    {
        var gestures = new MobileTouchGestures();
        int pinches = 0, releases = 0;
        gestures.PinchStarted += () => pinches++;
        gestures.PointerUp += _ => releases++;
        gestures.Down(1, 1, new(100, 100), false);
        gestures.Down(1, 2, new(200, 100), true);
        gestures.Move(1, 2, new(300, 100));
        gestures.Up(1, 1, new(100, 100));
        gestures.Up(1, 2, new(300, 100));
        Assert.Equal(0, pinches);
        Assert.Equal(1, releases);

        gestures.Down(1, 3, new(100, 100), true);
        gestures.Down(1, 4, new(200, 100), false);
        gestures.Up(1, 3, new(100, 100));
        gestures.Up(1, 4, new(200, 100));
        Assert.Equal(0, pinches);
        Assert.Equal(1, releases);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancellationOrFocusLossDoesNotClickAndAllowsTheNextGesture(bool focusLoss)
    {
        var gestures = new MobileTouchGestures();
        int canceled = 0, releases = 0;
        gestures.PointerCancel += () => canceled++;
        gestures.PointerUp += _ => releases++;
        gestures.Down(1, 1, new(100, 100), true);
        if (focusLoss) gestures.Reset();
        else gestures.Up(1, 1, new(100, 100), canceled: true);
        gestures.Up(1, 1, new(100, 100));
        Assert.Equal(1, canceled);
        Assert.Equal(0, releases);
        Assert.Equal(0, gestures.FingerCount);
        gestures.Down(1, 2, new(100, 100), true);
        gestures.Up(1, 2, new(100, 100));
        Assert.Equal(1, releases);
    }

    [Fact]
    public void ThirdFingerStopsZoomUntilAllFingersLift()
    {
        var gestures = new MobileTouchGestures();
        int scales = 0;
        gestures.PinchScale += _ => scales++;
        gestures.Down(1, 1, new(100, 100), true);
        gestures.Down(1, 2, new(200, 100), true);
        gestures.Down(1, 3, new(300, 100), true);
        gestures.Up(1, 3, new(300, 100));
        gestures.Move(1, 2, new(400, 100));
        Assert.Equal(0, scales);
    }

    [Fact]
    public void PinchUsesDistanceInWindowPointsAndRespectsCameraLimits()
    {
        var gestures = new MobileTouchGestures();
        var camera = new Camera(0.5f, 2f);
        float initialZoom = 0;
        gestures.PinchStarted += () => initialZoom = camera.Zoom;
        gestures.PinchScale += scale => camera.Zoom = initialZoom / scale;
        gestures.Down(1, 1, new(0, 0), true);
        gestures.Down(1, 2, new(60, 80), true);
        gestures.Move(1, 2, new(120, 160));
        Assert.Equal(0.5f, camera.Zoom);
        gestures.Move(1, 2, new(240, 320));
        Assert.Equal(0.5f, camera.Zoom);
        gestures.Move(1, 2, new(6, 8));
        Assert.Equal(2f, camera.Zoom);
        gestures.Move(1, 2, new(60, 80));
        Assert.Equal(1f, camera.Zoom);
    }

    [Fact]
    public void CoincidentFingersDoNotProduceAnInfiniteZoom()
    {
        var gestures = new MobileTouchGestures();
        var scales = new List<float>();
        gestures.PinchScale += scales.Add;
        gestures.Down(1, 1, new(100, 100), true);
        gestures.Down(1, 2, new(100, 100), true);
        gestures.Move(1, 2, new(120, 100));
        Assert.Empty(scales);
        gestures.Move(1, 2, new(140, 100));
        Assert.Equal(new[] { 2f }, scales);
    }
}
