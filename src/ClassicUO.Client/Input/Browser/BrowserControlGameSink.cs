using System;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;

namespace ClassicUO.Input;

/// <summary>
/// Default game sink for browser controls. It reuses the same player, target, and chat services as
/// desktop input. Context-menu behavior is supplied by the host because it depends on the currently
/// hovered game object.
/// </summary>
public sealed class BrowserControlGameSink : IBrowserControlSink
{
    private readonly World _world;
    private readonly Action _contextAction;

    /// <summary>Creates a sink for a live world.</summary>
    public BrowserControlGameSink(World world, Action contextAction)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _contextAction = contextAction ?? throw new ArgumentNullException(nameof(contextAction));
    }

    /// <inheritdoc />
    public void Move(Direction direction, bool pressed)
    {
        if (pressed && _world.InGame && _world.Player is not null)
            _world.Player.Walk(direction, false);
    }

    /// <inheritdoc />
    public void Target() => _world.TargetManager.SetTargeting(
        CursorTarget.Internal,
        CursorType.Target,
        TargetType.Neutral);

    /// <inheritdoc />
    public void Context() => _contextAction();

    /// <inheritdoc />
    public void Chat() => GameActions.OpenChat(_world);
}
