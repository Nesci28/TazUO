using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassicUO.Game.Data;

/// <summary>A named destination, including its facet so identical coordinates remain distinct.</summary>
public sealed class MapLocation
{
    public int MapId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public string Description { get; set; } = "";

    internal bool SamePosition(MapLocation other) =>
        other != null && MapId == other.MapId && X == other.X && Y == other.Y;
}

internal static class MapLocationHistory
{
    internal const int RecentLimit = 10;

    /// <summary>Moves a destination to the front, preserving its description on unnamed revisits.</summary>
    internal static List<MapLocation> Record(IEnumerable<MapLocation> locations, MapLocation location,
        int limit = RecentLimit)
    {
        List<MapLocation> result = locations?.Where(l => l != null).ToList() ?? [];
        MapLocation previous = result.FirstOrDefault(location.SamePosition);
        string description = location.Description?.Trim() ?? "";
        if (description.Length == 0)
            description = previous?.Description ?? "";

        result.RemoveAll(location.SamePosition);
        result.Insert(0, new MapLocation
        {
            MapId = location.MapId, X = location.X, Y = location.Y, Description = description
        });
        return result.Take(limit).ToList();
    }

    internal static bool Matches(MapLocation location, string search) =>
        string.IsNullOrWhiteSpace(search)
        || (location.Description ?? "").Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)
        || $"{location.X}, {location.Y}".Contains(search.Trim(), StringComparison.OrdinalIgnoreCase);
}
