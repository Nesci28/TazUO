using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using static ClassicUO.Game.UI.Gumps.WorldMapGump;

namespace ClassicUO.Game.Data;

internal static class MapLocationCatalog
{
    internal static IEnumerable<MapLocation> Markers(IEnumerable<WMapMarkerFile> files, int mapId,
        int zoomIndex, bool alwaysShowMarkers) => files.Where(f => !f.Hidden)
        .SelectMany(f => f.Markers)
        .Where(m => m.MapId == mapId
            && (alwaysShowMarkers || zoomIndex >= m.ZoomIndex || m.Color != Color.Transparent))
        .Select(FromMarker);

    internal static MapLocation FromMarker(WMapMarker marker) => new()
    {
        MapId = marker.MapId, X = marker.X, Y = marker.Y, Description = marker.Name ?? ""
    };
}
