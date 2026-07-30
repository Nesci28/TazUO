using System.Xml;
using ClassicUO.Assets;
using ClassicUO.Game.UI;
using Xunit;

namespace ClassicUO.UnitTests.Game.UI;

public class XmlGumpHandlerTests
{
    [Fact]
    public void ParsePlayerPaperDollSettings_UsesDefaults()
    {
        XmlNode node = LoadNode("<player_paperdoll />");

        PlayerPaperDollSettings settings = XmlGumpHandler.ParsePlayerPaperDollSettings(node);

        Assert.Equal(PlayerPaperDollSettings.DefaultWidth, settings.Width);
        Assert.Equal(PlayerPaperDollSettings.DefaultHeight, settings.Height);
        Assert.True(settings.Updates);
        Assert.False(settings.Background);
        Assert.Equal(1f, settings.Alpha);
    }

    [Fact]
    public void ParsePlayerPaperDollSettings_ParsesSupportedAttributes()
    {
        XmlNode node = LoadNode(
            "<player_paperdoll width=\"72\" height=\"68\" updates=\"false\" background=\"true\" alpha=\"0.65\" />"
        );

        PlayerPaperDollSettings settings = XmlGumpHandler.ParsePlayerPaperDollSettings(node);

        Assert.Equal(72, settings.Width);
        Assert.Equal(68, settings.Height);
        Assert.False(settings.Updates);
        Assert.True(settings.Background);
        Assert.Equal(0.65f, settings.Alpha);
    }

    [Fact]
    public void ParsePlayerPaperDollSettings_RejectsInvalidSizesAndClampsAlpha()
    {
        XmlNode node = LoadNode(
            "<player_paperdoll width=\"0\" height=\"invalid\" updates=\"invalid\" background=\"invalid\" alpha=\"3\" />"
        );

        PlayerPaperDollSettings settings = XmlGumpHandler.ParsePlayerPaperDollSettings(node);

        Assert.Equal(PlayerPaperDollSettings.DefaultWidth, settings.Width);
        Assert.Equal(PlayerPaperDollSettings.DefaultHeight, settings.Height);
        Assert.True(settings.Updates);
        Assert.False(settings.Background);
        Assert.Equal(1f, settings.Alpha);
    }

    [Fact]
    public void ParseEmbeddedTextureSettings_ParsesSupportedAttributes()
    {
        XmlNode node = LoadNode(
            "<nine_slice texture=\"LegionXmlWindow.png\" border=\"10\" hue=\"67\" alpha=\"0.75\" />"
        );

        EmbeddedTextureSettings settings = XmlGumpHandler.ParseEmbeddedTextureSettings(node, 8);

        Assert.Equal("LegionXmlWindow.png", settings.Texture);
        Assert.Equal(10, settings.Border);
        Assert.Equal((ushort)67, settings.Hue);
        Assert.Equal(0.75f, settings.Alpha);
    }

    [Fact]
    public void ParseEmbeddedTextureSettings_UsesSafeDefaultsAndClampsAlpha()
    {
        XmlNode node = LoadNode(
            "<embedded_image name=\"LegionXmlPortraitFrame.png\" border=\"0\" hue=\"invalid\" alpha=\"-2\" />"
        );

        EmbeddedTextureSettings settings = XmlGumpHandler.ParseEmbeddedTextureSettings(node, 8);

        Assert.Equal("LegionXmlPortraitFrame.png", settings.Texture);
        Assert.Equal(8, settings.Border);
        Assert.Equal((ushort)0, settings.Hue);
        Assert.Equal(0f, settings.Alpha);
    }

    [Theory]
    [InlineData("LegionXmlWindow.png")]
    [InlineData("LegionXmlPanel.png")]
    [InlineData("LegionXmlInset.png")]
    [InlineData("LegionXmlPortraitFrame.png")]
    [InlineData("LegionXmlTitleGem.png")]
    public void LegionXmlThemeAssets_AreEmbedded(string fileName)
    {
        string resourceName = "ClassicUO.Assets.gumpartassets." + fileName;

        Assert.Contains(resourceName, typeof(ExternalImageLoader).Assembly.GetManifestResourceNames());
    }

    private static XmlNode LoadNode(string xml)
    {
        var document = new XmlDocument();
        document.LoadXml(xml);
        return document.DocumentElement;
    }
}
