using System;
using ClassicUO.Game.Data;

namespace ClassicUO.Input;

/// <summary>Portable actions emitted by the browser touch controls.</summary>
public enum BrowserControlAction
{
    Unknown,
    MoveNorth,
    MoveSouth,
    MoveEast,
    MoveWest,
    Target,
    Context,
    Chat
}

/// <summary>A validated browser control event ready for the client input dispatcher.</summary>
public readonly record struct BrowserControlEvent(BrowserControlAction Action, string Phase);

/// <summary>Receives validated browser actions and applies them to the game input layer.</summary>
public interface IBrowserControlSink
{
    /// <summary>Changes the held state of a movement direction.</summary>
    void Move(Direction direction, bool pressed);

    /// <summary>Starts targeting.</summary>
    void Target();

    /// <summary>Opens a context action.</summary>
    void Context();

    /// <summary>Opens chat input.</summary>
    void Chat();
}

/// <summary>Dispatches browser controls to the existing game input actions.</summary>
public sealed class BrowserControlDispatcher(IBrowserControlSink sink)
{
    private readonly IBrowserControlSink _sink = sink ?? throw new ArgumentNullException(nameof(sink));

    /// <summary>Dispatches an event. Invalid or unsupported events are ignored.</summary>
    public void Dispatch(BrowserControlEvent controlEvent)
    {
        bool release = string.Equals(controlEvent.Phase, "up", StringComparison.Ordinal);
        bool activate = !release && controlEvent.Phase is "down" or "press" or "longpress";

        switch (controlEvent.Action)
        {
            case BrowserControlAction.MoveNorth:
                _sink.Move(Direction.North, !release);
                break;
            case BrowserControlAction.MoveSouth:
                _sink.Move(Direction.South, !release);
                break;
            case BrowserControlAction.MoveEast:
                _sink.Move(Direction.East, !release);
                break;
            case BrowserControlAction.MoveWest:
                _sink.Move(Direction.West, !release);
                break;
            case BrowserControlAction.Target when activate:
                _sink.Target();
                break;
            case BrowserControlAction.Context when activate:
                _sink.Context();
                break;
            case BrowserControlAction.Chat when activate:
                _sink.Chat();
                break;
        }
    }
}

/// <summary>Converts browser action names into the existing input action vocabulary.</summary>
public static class BrowserControlActionParser
{
    /// <summary>Parses an action such as <c>move:north</c> or <c>target</c>.</summary>
    public static BrowserControlAction Parse(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
            return BrowserControlAction.Unknown;

        return action.Trim().ToLowerInvariant() switch
        {
            "move:north" => BrowserControlAction.MoveNorth,
            "move:south" => BrowserControlAction.MoveSouth,
            "move:east" => BrowserControlAction.MoveEast,
            "move:west" => BrowserControlAction.MoveWest,
            "target" => BrowserControlAction.Target,
            "context" => BrowserControlAction.Context,
            "chat" => BrowserControlAction.Chat,
            _ => BrowserControlAction.Unknown
        };
    }

    /// <summary>Parses an action and phase received from the browser event bridge.</summary>
    public static bool TryParse(string action, string phase, out BrowserControlEvent controlEvent)
    {
        BrowserControlAction parsedAction = Parse(action);
        if (parsedAction == BrowserControlAction.Unknown || string.IsNullOrWhiteSpace(phase))
        {
            controlEvent = default;
            return false;
        }

        controlEvent = new BrowserControlEvent(parsedAction, phase.Trim().ToLowerInvariant());
        return true;
    }
}
