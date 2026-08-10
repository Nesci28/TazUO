using ClassicUO.Game.Managers.VendorSearch;
using FluentAssertions;
using System.Collections.Generic;
using Xunit;

namespace ClassicUO.UnitTests.Game.Managers;

public class VendorSearchPacketAnalyzerTests
{
    [Theory]
    [InlineData("{xmfhtmltok 10 10 760 18 0 0 19389 1114513 @#1154508@}", "Query")]
    [InlineData("{xmfhtmltok 50 50 400 18 0 0 19389 1114513 @#1154509@}", "Results")]
    [InlineData("{xmfhtmltok 27 47 380 80 0 0 20083 1114513 @#1154678@}", "Waiting")]
    public void ClassifiesVendorSearchClilocMarkers(
        string layout,
        string expected
    )
    {
        VendorSearchPacketAnalyzer
            .Classify(layout)
            .ToString()
            .Should()
            .Be(expected);
    }

    [Fact]
    public void DoesNotMatchMarkerEmbeddedInAnotherNumber()
    {
        VendorSearchPacketAnalyzer
            .Classify("{xmfhtmlgump 0 0 100 20 111545080 0 0}")
            .Should()
            .Be(VendorSearchGumpKind.None);
    }

    [Fact]
    public void FallsBackToVisibleTitleForTextBasedShardGumps()
    {
        VendorSearchPacketAnalyzer
            .Classify("{htmlgump 0 0 200 20 0 0 0}", ["<div>Vendor Search Results</div>"])
            .Should()
            .Be(VendorSearchGumpKind.Results);
    }

    [Fact]
    public void AssociatesItemPropertiesThatPrecedeButtonTileArtByOrdinal()
    {
        const string layout = """
            {page 0}
            {itemproperty 0x40000001}
            {buttontileart 50 101 2328 2328 0 0 0 0x1087 0 8 8}
            {itemproperty 0x40000002}
            {buttontileart 50 176 2328 2328 0 0 0 0x13B9 1150 7 9}
            """;

        var items = VendorSearchPacketAnalyzer.AnalyzeItems(layout);

        items.Should().HaveCount(2);
        items[0].Serial.Should().Be(0x40000001);
        items[0].Graphic.Should().Be(0x1087);
        items[0].X.Should().Be(50);
        items[0].TileOffsetX.Should().Be(8);
        items[1].Serial.Should().Be(0x40000002);
        items[1].Graphic.Should().Be(0x13B9);
        items[1].Hue.Should().Be(1150);
    }

    [Fact]
    public void AssociatesTilePicAsGumpPicWithFollowingItemProperty()
    {
        const string layout = """
            {page 2}
            {tilepicasgumppic 96 350 0x1087 hue=42}
            {resizepic 88 342 2620 160 120}
            {itemproperty 0x40000001}
            """;

        var items = VendorSearchPacketAnalyzer.AnalyzeItems(layout);

        items.Should().ContainSingle();
        items[0].Page.Should().Be(2);
        items[0].Serial.Should().Be(0x40000001);
        items[0].Graphic.Should().Be(0x1087);
        items[0].Hue.Should().Be(42);
    }

    [Fact]
    public void NormalizesServerHtmlWithoutLeavingExecutableMarkup()
    {
        VendorSearchPacketAnalyzer
            .NormalizeText("<DIV ALIGN=CENTER>Sword &amp; Shield<br/>Damage 20%</DIV>")
            .Should()
            .Be("Sword & Shield\nDamage 20%");
    }

    [Fact]
    public void RejectsStaleOrUnlistedGumpResponses()
    {
        VendorSearchSnapshot snapshot = CreateResponseSnapshot();

        VendorSearchResponseValidator
            .TryValidate(
                snapshot,
                new VendorSearchResponseRequest { Version = 9, ButtonID = 1 },
                out int staleStatus,
                out _
            )
            .Should()
            .BeFalse();
        staleStatus.Should().Be(409);

        VendorSearchResponseValidator
            .TryValidate(
                snapshot,
                new VendorSearchResponseRequest { Version = 10, ButtonID = 50 },
                out int pageButtonStatus,
                out _
            )
            .Should()
            .BeFalse("client-side page buttons are never valid 0xB1 replies");
        pageButtonStatus.Should().Be(400);
    }

    [Fact]
    public void AcceptsOnlyCurrentListedControlsAndPacketSizedText()
    {
        VendorSearchSnapshot snapshot = CreateResponseSnapshot();
        var valid = new VendorSearchResponseRequest
        {
            Version = 10,
            ButtonID = 1,
            Entries = new Dictionary<int, string> { [1] = "katana" },
            Switches = [7]
        };

        VendorSearchResponseValidator
            .TryValidate(snapshot, valid, out _, out _)
            .Should()
            .BeTrue();

        valid.Entries[1] = new string('x', 240);
        VendorSearchResponseValidator
            .TryValidate(snapshot, valid, out int oversizedStatus, out _)
            .Should()
            .BeFalse();
        oversizedStatus.Should().Be(400);
    }

    private static VendorSearchSnapshot CreateResponseSnapshot() =>
        new()
        {
            Version = 10,
            Kind = VendorSearchGumpKind.Query,
            Buttons =
            [
                new VendorSearchButtonControl { ButtonID = 1 },
                new VendorSearchButtonControl
                {
                    ButtonID = 50,
                    IsPageButton = true,
                    ToPage = 2
                }
            ],
            Entries = [new VendorSearchEntryControl { ID = 1 }],
            Switches = [new VendorSearchSwitchControl { ID = 7 }]
        };
}
