using System.Collections.Generic;
using ClassicUO.Input;
using ClassicUO.Game.Data;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.Input;

public sealed class BrowserControlActionTests
{
    [Theory]
    [InlineData("move:north", BrowserControlAction.MoveNorth)]
    [InlineData("MOVE:WEST", BrowserControlAction.MoveWest)]
    [InlineData("target", BrowserControlAction.Target)]
    [InlineData("context", BrowserControlAction.Context)]
    [InlineData("chat", BrowserControlAction.Chat)]
    public void ParsesSupportedActions(string action, BrowserControlAction expected)
    {
        BrowserControlActionParser.Parse(action).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("move:up")]
    [InlineData("javascript:alert(1)")]
    public void RejectsUnknownActions(string action)
    {
        BrowserControlActionParser.Parse(action).Should().Be(BrowserControlAction.Unknown);
    }

    [Fact]
    public void ParsesActionAndPhaseForDispatcher()
    {
        BrowserControlActionParser.TryParse("move:north", "DOWN", out BrowserControlEvent controlEvent)
            .Should().BeTrue();
        controlEvent.Action.Should().Be(BrowserControlAction.MoveNorth);
        controlEvent.Phase.Should().Be("down");
    }

    [Fact]
    public void RejectsUnknownActionEvents()
    {
        BrowserControlActionParser.TryParse("move:up", "press", out _).Should().BeFalse();
    }

    [Fact]
    public void DispatchesMovementPressAndRelease()
    {
        var sink = new RecordingSink();
        var dispatcher = new BrowserControlDispatcher(sink);

        dispatcher.Dispatch(new BrowserControlEvent(BrowserControlAction.MoveNorth, "down"));
        dispatcher.Dispatch(new BrowserControlEvent(BrowserControlAction.MoveNorth, "up"));

        sink.LastDirection.Should().Be(Direction.North);
        sink.MoveStates.Should().Equal(true, false);
    }

    [Fact]
    public void DispatchesActionOnlyOnActivationPhase()
    {
        var sink = new RecordingSink();
        var dispatcher = new BrowserControlDispatcher(sink);

        dispatcher.Dispatch(new BrowserControlEvent(BrowserControlAction.Target, "down"));
        dispatcher.Dispatch(new BrowserControlEvent(BrowserControlAction.Target, "up"));

        sink.TargetCalls.Should().Be(1);
    }

    private sealed class RecordingSink : IBrowserControlSink
    {
        public Direction LastDirection { get; private set; }
        public List<bool> MoveStates { get; } = [];
        public int TargetCalls { get; private set; }

        public void Move(Direction direction, bool pressed)
        {
            LastDirection = direction;
            MoveStates.Add(pressed);
        }

        public void Target() => TargetCalls++;
        public void Context() { }
        public void Chat() { }
    }
}
