using System.Reflection;
using System.Runtime.CompilerServices;
using ClassicUO.Game;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;
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
    public void ResolvesFollowingItemPropertyWhenActualItemArtIsHovered()
    {
        var parent = new TestControl();
        ButtonTileArt itemArt = CreateButtonTileArt(EarringsGraphic);
        var propertyControl = new TestControl();

        parent.Add(itemArt);
        parent.Add(propertyControl);
        propertyControl.SetItemPropertyTooltip(VendorItemSerial, EarringsGraphic);

        bool resolved = GameCursor.TryResolveItemProperty(
            itemArt,
            out uint serial,
            out ushort graphic
        );

        resolved.Should().BeTrue();
        serial.Should().Be(VendorItemSerial);
        graphic.Should().Be(EarringsGraphic);
    }

    [Fact]
    public void ResolvesTilePicAsGumpPicFromNormalItemPropertyTooltip()
    {
        GumpPic itemArt = CreateGumpPic(EarringsGraphic);
        itemArt.SetTooltip(VendorItemSerial);

        bool resolved = GameCursor.TryResolveItemProperty(itemArt, out uint serial, out ushort graphic);

        resolved.Should().BeTrue();
        serial.Should().Be(VendorItemSerial);
        graphic.Should().Be(EarringsGraphic);
    }

    [Fact]
    public void ResolvesTilePicAsGumpPicFromPacketTextWhenOverlayHasTooltip()
    {
        var gump = new Gump(null, 0, 1)
        {
            PacketGumpText = $"""
                Vendor Search Results
                tilepicasgumppic 96 350 0x{EarringsGraphic:X4} 0 0 0 0
                resizepic 88 342 2620 160 120
                itemproperty 0x{VendorItemSerial:X8}
                """
        };
        var overlay = new TestControl { Parent = gump };
        overlay.SetTooltip(VendorItemSerial);

        bool resolved = GameCursor.TryResolveItemProperty(
            overlay,
            out uint serial,
            out ushort graphic
        );

        resolved.Should().BeTrue();
        serial.Should().Be(VendorItemSerial);
        graphic.Should().Be(EarringsGraphic);
    }

    [Fact]
    public void ResolvesButtonTileArtFromPacketTextWhenItemPropertyPrecedesVisual()
    {
        string packetGumpText = $"""
            itemproperty {VendorItemSerial}
            buttontileart 88 342 0 1 0 0 0 0x{EarringsGraphic:X4} 0 8 8
            """;

        bool resolved = GameCursor.TryResolveItemGraphicFromGumpText(
            packetGumpText,
            VendorItemSerial,
            out ushort graphic
        );

        resolved.Should().BeTrue();
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

    private static GumpPic CreateGumpPic(ushort graphic)
    {
        var itemArt = (GumpPic)RuntimeHelpers.GetUninitializedObject(typeof(GumpPic));
        typeof(GumpPicBase)
            .GetField("_graphic", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(itemArt, graphic);
        return itemArt;
    }

    private sealed class TestControl : Control { }
}
