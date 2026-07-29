using System.Xml;
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

    private static XmlNode LoadNode(string xml)
    {
        var document = new XmlDocument();
        document.LoadXml(xml);
        return document.DocumentElement;
    }
}
