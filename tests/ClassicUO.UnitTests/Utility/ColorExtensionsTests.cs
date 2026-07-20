using ClassicUO.Utility;
using FluentAssertions;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.Utility;

public class ColorExtensionsTests
{
    [Fact]
    public void FromHtmlHexReadsRgbAndRgba()
    {
        "#123456".FromHtmlHex(Color.Red).Should().Be(new Color(0x12, 0x34, 0x56, 0xFF));
        "#12345678".FromHtmlHex(Color.Red).Should().Be(new Color(0x12, 0x34, 0x56, 0x78));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    public void FromHtmlHexReturnsFallbackForInvalidInput(string value)
    {
        Color fallback = new(1, 2, 3, 4);

        value.FromHtmlHex(fallback).Should().Be(fallback);
    }

    [Fact]
    public void GridHighlightColorRoundTripsAlpha()
    {
        var entry = new ClassicUO.Game.UI.Gumps.GridHighLight.GridHighlightSetupEntry();
        Color expected = new(12, 34, 56, 78);

        entry.SetHighlightColor(expected);

        entry.HighlightColor.Should().Be("#0C22384E");
        entry.GetHighlightColor().Should().Be(expected);
    }
}
