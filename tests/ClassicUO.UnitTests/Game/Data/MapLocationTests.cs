using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.UI.MyraWindows;
using FluentAssertions;
using Microsoft.Xna.Framework;
using Xunit;
using static ClassicUO.Game.UI.Gumps.WorldMapGump;

namespace ClassicUO.UnitTests.Game.Data;

public class MapLocationTests
{
    [Fact]
    public void RecentHistoryKeepsTenDistinctDestinationsNewestFirst()
    {
        List<MapLocation> history = [];
        for (int x = 0; x < 12; x++)
            history = MapLocationHistory.Record(history, Location(x));

        history.Select(l => l.X).Should().Equal(11, 10, 9, 8, 7, 6, 5, 4, 3, 2);
        history = MapLocationHistory.Record(history, Location(4, "Home"));
        history.Select(l => l.X).Should().Equal(4, 11, 10, 9, 8, 7, 6, 5, 3, 2);
        history[0].Description.Should().Be("Home");
    }

    [Fact]
    public void RevisitingWithoutADescriptionKeepsThePreviousName()
    {
        List<MapLocation> original = [Location(1, "Mine")];
        List<MapLocation> revisited = MapLocationHistory.Record(original, Location(1, "  "));
        revisited.Should().ContainSingle().Which.Description.Should().Be("Mine");
        revisited[0].Should().NotBeSameAs(original[0]);
        MapLocationHistory.Record(revisited, Location(1, "  New mine  "))[0].Description.Should().Be("New mine");
        original[0].Description.Should().Be("Mine");
    }

    [Fact]
    public void IdenticalCoordinatesOnDifferentFacetsRemainSeparate()
    {
        var location = Location(1);
        location.MapId = 1;
        MapLocationHistory.Record([Location(1)], location).Select(l => l.MapId).Should().Equal(1, 0);
    }

    [Fact]
    public void SavedDestinationsAreNotLimitedByRecentHistory()
    {
        List<MapLocation> saved = [];
        for (int x = 0; x < 15; x++)
            saved = MapLocationHistory.Record(saved, Location(x, "Saved"), int.MaxValue);
        saved.Should().HaveCount(15);
        saved = MapLocationHistory.Record(saved, Location(3, "Renamed"), int.MaxValue);
        saved.Should().HaveCount(15);
        saved.Single(l => l.X == 3).Description.Should().Be("Renamed");
    }

    [Fact]
    public void ProfileJsonRoundTripsBothListsAndDescriptions()
    {
        var profile = new Profile
        {
            WorldMapRecentLocations = [Location(1, "Mine, entrée\nEast")],
            WorldMapSavedLocations = [new MapLocation { MapId = 2, X = 123, Y = 456, Description = "Home" }]
        };
        var metadata = ProfileJsonContext.DefaultToUse.Profile;
        string json = JsonSerializer.Serialize(profile, metadata);
        // Other profile setters initialize the graphics UI. Hydrate the destination fields
        // with the real profile metadata without requiring a running game for this test.
        JsonObject stored = JsonNode.Parse(json).AsObject();
        var destinations = new JsonObject
        {
            ["world_map_recent_locations"] = stored["world_map_recent_locations"].DeepClone(),
            ["world_map_saved_locations"] = stored["world_map_saved_locations"].DeepClone()
        };
        Profile loaded = JsonSerializer.Deserialize(destinations.ToJsonString(), metadata);
        loaded.WorldMapRecentLocations.Should().BeEquivalentTo(profile.WorldMapRecentLocations);
        loaded.WorldMapSavedLocations.Should().BeEquivalentTo(profile.WorldMapSavedLocations);

        Profile legacy = JsonSerializer.Deserialize("{}", metadata);
        legacy.WorldMapRecentLocations.Should().BeEmpty();
        legacy.WorldMapSavedLocations.Should().BeEmpty();
        MapLocationHistory.Record(null, Location(1)).Should().ContainSingle();
        MapLocationHistory.Record([null], Location(1)).Should().ContainSingle();
    }

    [Fact]
    public void FilteredNodesRespectCategoryFacetAndZoomVisibility()
    {
        WMapMarkerFile enabled = new()
        {
            Markers =
            [
                Marker("Visible dot", 0, Color.Yellow, 8),
                Marker("Hidden by zoom", 0, Color.Transparent, 8),
                Marker("Visible icon", 0, Color.Transparent, 3),
                Marker("Other facet", 1, Color.Yellow, 3)
            ]
        };
        WMapMarkerFile hidden = new() { Hidden = true, Markers = [Marker("Hidden file", 0, Color.Yellow, 3)] };
        MapLocationCatalog.Markers([enabled, hidden], 0, 4, false).Select(l => l.Description)
            .Should().Equal("Visible dot", "Visible icon");
        MapLocationCatalog.Markers([enabled, hidden], 0, 4, true).Select(l => l.Description)
            .Should().Equal("Visible dot", "Hidden by zoom", "Visible icon");
        enabled.Hidden = true;
        MapLocationCatalog.Markers([enabled, hidden], 0, 4, true).Should().BeEmpty();
    }

    [Theory]
    [InlineData(" 1639, 1532 ", 1639, 1532)]
    [InlineData("1331:745", 1331, 745)]
    [InlineData("123 4141", 123, 4141)]
    [InlineData("0, 0", 0, 0)]
    public void RawCoordinatesAcceptExistingFormats(string text, int x, int y)
    {
        LocationGoWindow.ParsePoint(text, out Point point).Should().BeTrue();
        point.Should().Be(new Point(x, y));
    }

    [Theory]
    [InlineData("999999999999999999999, 12")]
    [InlineData("12, 999999999999999999999")]
    [InlineData("-1, 12")]
    [InlineData("hello")]
    [InlineData("12,")]
    [InlineData(null)]
    public void InvalidCoordinatesAreRejectedWithoutThrowing(string text)
    {
        LocationGoWindow.ParsePoint(text, out Point point).Should().BeFalse();
        point.Should().Be(Sextant.InvalidPoint);
    }

    [Theory]
    [InlineData(" mine ", true)]
    [InlineData("123, 456", true)]
    [InlineData("missing", false)]
    public void SearchMatchesDescriptionsAndCoordinates(string search, bool expected) =>
        MapLocationHistory.Matches(Location(123, "North Mine"), search).Should().Be(expected);

    private static MapLocation Location(int x, string description = "") =>
        new() { MapId = 0, X = x, Y = 456, Description = description };

    private static WMapMarker Marker(string name, int map, Color color, int zoom) =>
        new() { Name = name, MapId = map, Color = color, ZoomIndex = zoom, X = 123, Y = 456 };
}
