using System.Collections;
using System.Linq;
using ClassicUO.Assets;
using ClassicUO.LegionScripting;
using ClassicUO.LegionScripting.ApiClasses;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.Game.LegionScript;

public class ApiHousingPlacementTests
{
    [Fact]
    public void BuildOfficialHousePlacementAreas_SouthFacing_UsesExpectedAtlanticClearanceBands()
    {
        var areas = LegionAPI.BuildOfficialHousePlacementAreas(100, 200, 3, 2, "south", 6, 5, 1, true).ToArray();

        areas.Should().HaveCount(5);
        areas[0].Should().Match<LegionAPI.HousePlacementArea>(a =>
            a.Name == "step" && a.MinX == 100 && a.MaxX == 102 && a.MinY == 202 && a.MaxY == 202 && a.RequiresBuildableLand);
        areas[1].Should().Match<LegionAPI.HousePlacementArea>(a =>
            a.Name == "front" && a.MinX == 100 && a.MaxX == 102 && a.MinY == 203 && a.MaxY == 207 && !a.RequiresBuildableLand);
        areas[2].Should().Match<LegionAPI.HousePlacementArea>(a =>
            a.Name == "back" && a.MinX == 100 && a.MaxX == 102 && a.MinY == 195 && a.MaxY == 199);
        areas[3].Should().Match<LegionAPI.HousePlacementArea>(a =>
            a.Name == "left" && a.MinX == 103 && a.MaxX == 103 && a.MinY == 200 && a.MaxY == 201);
        areas[4].Should().Match<LegionAPI.HousePlacementArea>(a =>
            a.Name == "right" && a.MinX == 99 && a.MaxX == 99 && a.MinY == 200 && a.MaxY == 201);
    }

    [Fact]
    public void BuildOfficialHousePlacementAreas_EastFacing_RotatesFrontBackAndSides()
    {
        var areas = LegionAPI.BuildOfficialHousePlacementAreas(10, 20, 4, 3, "east", 6, 5, 1, true).ToArray();

        areas.Should().HaveCount(5);
        areas[0].Should().Match<LegionAPI.HousePlacementArea>(a =>
            a.Name == "step" && a.MinX == 14 && a.MaxX == 14 && a.MinY == 20 && a.MaxY == 22);
        areas[1].Should().Match<LegionAPI.HousePlacementArea>(a =>
            a.Name == "front" && a.MinX == 15 && a.MaxX == 19 && a.MinY == 20 && a.MaxY == 22);
        areas[2].Should().Match<LegionAPI.HousePlacementArea>(a =>
            a.Name == "back" && a.MinX == 5 && a.MaxX == 9 && a.MinY == 20 && a.MaxY == 22);
        areas[3].Should().Match<LegionAPI.HousePlacementArea>(a =>
            a.Name == "left" && a.MinX == 10 && a.MaxX == 13 && a.MinY == 19 && a.MaxY == 19);
        areas[4].Should().Match<LegionAPI.HousePlacementArea>(a =>
            a.Name == "right" && a.MinX == 10 && a.MaxX == 13 && a.MinY == 23 && a.MaxY == 23);
    }

    [Fact]
    public void BuildOfficialHousePlacementAreas_WhenStepsDisabled_UsesSingleFrontBand()
    {
        var areas = LegionAPI.BuildOfficialHousePlacementAreas(100, 200, 3, 2, "south", 6, 5, 1, false).ToArray();

        areas.Select(a => a.Name).Should().Equal("front", "back", "left", "right");
        areas[0].Should().Match<LegionAPI.HousePlacementArea>(a =>
            a.MinX == 100 && a.MaxX == 102 && a.MinY == 202 && a.MaxY == 207 && !a.RequiresBuildableLand);
    }

    [Theory]
    [InlineData(0x0071, true)]
    [InlineData(0x0078, true)]
    [InlineData(0x0079, false)]
    [InlineData(0x0442, true)]
    [InlineData(0x0479, true)]
    [InlineData(0x047A, false)]
    [InlineData(0x3FF4, true)]
    [InlineData(0x3FF5, false)]
    public void IsRoadCandidate_UsesRunUoGraphicRanges(ushort graphic, bool expected)
    {
        LegionAPI.IsRoadCandidate(graphic, "irrelevant", 0).Should().Be(expected);
    }

    [Fact]
    public void BuildRunUoCustomHouseCatalog_MatchesOfficialFoundationCountsAndBoundaries()
    {
        var catalog = LegionAPI.BuildRunUoCustomHouseCatalog();

        catalog.Should().HaveCount(102);
        catalog.Count(entry => entry.Stories == 2).Should().Be(47);
        catalog.Count(entry => entry.Stories == 3).Should().Be(55);

        catalog.Should().ContainSingle(entry =>
            entry.Width == 7 && entry.Depth == 7 && entry.Stories == 2 && entry.MultiID == 0x13EC && entry.TargetOffsetY == 4);
        catalog.Should().ContainSingle(entry =>
            entry.Width == 13 && entry.Depth == 13 && entry.Stories == 2 && entry.MultiID == 0x143A && entry.TargetOffsetY == 7);
        catalog.Should().ContainSingle(entry =>
            entry.Width == 9 && entry.Depth == 14 && entry.Stories == 3 && entry.MultiID == 0x140B && entry.TargetOffsetY == 8);
        catalog.Should().ContainSingle(entry =>
            entry.Width == 18 && entry.Depth == 18 && entry.Stories == 3 && entry.MultiID == 0x147B && entry.TargetOffsetY == 10);
    }

    [Fact]
    public void BuildRunUoCustomHouseCatalog_DoesNotInventInvalidSizes()
    {
        var catalog = LegionAPI.BuildRunUoCustomHouseCatalog();

        catalog.Should().NotContain(entry => entry.Width == 13 && entry.Depth == 7);
        catalog.Should().NotContain(entry => entry.Width == 9 && entry.Depth == 15);
        catalog.Should().Contain(entry => entry.Width == 14 && entry.Depth == 9);
        catalog.Should().Contain(entry => entry.Width == 17 && entry.Depth == 12);
    }

    [Fact]
    public void TryGetRunUoFoundationBounds_ValidatesRawMultiBeforeAddingStairRow()
    {
        LegionAPI.RunUoFoundationCatalogEntry catalog = LegionAPI.BuildRunUoCustomHouseCatalog()
            .Single(entry => entry.Width == 7 && entry.Depth == 7 && entry.MultiID == 0x13EC);
        MultiInfo[] rawMulti =
        [
            new MultiInfo { X = -3, Y = -3 },
            new MultiInfo { X = 3, Y = 3 }
        ];

        LegionAPI.TryGetRunUoFoundationBounds(
            catalog,
            rawMulti,
            out int minX,
            out int minY,
            out int maxX,
            out int foundationMaxY,
            out int stairsY).Should().BeTrue();

        (minX, minY, maxX, foundationMaxY, stairsY).Should().Be((-3, -3, 3, 3, 4));
        (maxX - minX + 1).Should().Be(catalog.Width);
        (foundationMaxY - minY + 1).Should().Be(catalog.Depth);
        (stairsY - minY + 1).Should().Be(catalog.Depth + 1);
    }

    [Fact]
    public void TryGetTilePoint_ReadsPythonCompatibleListsAndPointObjects()
    {
        LegionAPI.TryGetTilePoint(new ArrayList { 123, 456 }, out int listX, out int listY).Should().BeTrue();
        (listX, listY).Should().Be((123, 456));

        LegionAPI.TryGetTilePoint(new ApiPoint3D { X = 789, Y = 321 }, out int pointX, out int pointY).Should().BeTrue();
        (pointX, pointY).Should().Be((789, 321));
    }
}
