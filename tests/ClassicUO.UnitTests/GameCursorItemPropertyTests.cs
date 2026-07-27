using System.Reflection;
using System.Runtime.CompilerServices;
using ClassicUO.Game;
using ClassicUO.Game.UI.Controls;
using FluentAssertions;
using SDL3;
using Xunit;

namespace ClassicUO.UnitTests.Game;

public class GameCursorItemPropertyTests
{
    private const uint VendorItemSerial = 0x40000001;
    private const ushort EarringsGraphic = 0x1087;

    [Fact]
    public void ResolvesDirectButtonTileArtFromNormalItemPropertyTooltip()
    {
        ButtonTileArt itemArt = CreateButtonTileArt(EarringsGraphic);
        itemArt.SetTooltip(VendorItemSerial);

        bool resolved = GameCursor.TryResolveItemProperty(itemArt, out uint serial, out ushort graphic);

        resolved.Should().BeTrue();
        serial.Should().Be(VendorItemSerial);
        graphic.Should().Be(EarringsGraphic);
    }

    [Fact]
    public void ResolvesPrecedingButtonTileArtWhenTooltipIsAttachedToAnotherControl()
    {
        var parent = new TestControl();
        ButtonTileArt itemArt = CreateButtonTileArt(EarringsGraphic);
        var tooltipControl = new TestControl();

        itemArt.Parent = parent;
        tooltipControl.Parent = parent;
        tooltipControl.SetTooltip(VendorItemSerial);

        bool resolved = GameCursor.TryResolveItemProperty(
            tooltipControl,
            out uint serial,
            out ushort graphic
        );

        resolved.Should().BeTrue();
        serial.Should().Be(VendorItemSerial);
        graphic.Should().Be(EarringsGraphic);
    }

    [Fact]
    public void CtrlActivatesVendorComparisonWithoutRegisteredGridHotkey()
    {
        var keyDown = new SDL.SDL_KeyboardEvent
        {
            key = (uint)SDL.SDL_Keycode.SDLK_LCTRL,
            mod = SDL.SDL_Keymod.SDL_KMOD_LCTRL
        };

        var keyUp = new SDL.SDL_KeyboardEvent
        {
            key = (uint)SDL.SDL_Keycode.SDLK_LCTRL,
            mod = SDL.SDL_Keymod.SDL_KMOD_NONE
        };

        try
        {
            ClassicUO.Input.Keyboard.OnKeyDown(keyDown);
            GameCursor.IsItemComparisonPressed(null).Should().BeTrue();
        }
        finally
        {
            ClassicUO.Input.Keyboard.OnKeyUp(keyUp);
        }
    }

    private static ButtonTileArt CreateButtonTileArt(ushort graphic)
    {
        var itemArt = (ButtonTileArt)RuntimeHelpers.GetUninitializedObject(typeof(ButtonTileArt));
        typeof(ButtonTileArt)
            .GetField("_graphic", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(itemArt, graphic);
        return itemArt;
    }

    private sealed class TestControl : Control { }
}
