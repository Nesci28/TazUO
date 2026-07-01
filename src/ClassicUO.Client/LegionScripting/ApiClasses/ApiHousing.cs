using System.Collections.Generic;

namespace ClassicUO.LegionScripting.ApiClasses;

/// <summary>
/// Tile flag metadata exposed to Legion scripts and MCP clients.
/// </summary>
public class ApiTileFlagInfo
{
    public ulong Value { get; set; }
    public string Names { get; set; } = string.Empty;
    public bool IsSurface { get; set; }
    public bool IsBridge { get; set; }
    public bool IsWet { get; set; }
    public bool IsFoliage { get; set; }
    public bool IsWall { get; set; }
    public bool IsDoor { get; set; }
    public bool IsImpassable { get; set; }
    public bool IsNoHouse { get; set; }
    public bool IsNoDiagonal { get; set; }
    public bool IsRoof { get; set; }
    public bool IsBackground { get; set; }
}

/// <summary>
/// Detailed land tile data at one map coordinate.
/// </summary>
public class ApiLandTileInfo
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public ushort Graphic { get; set; }
    public string GraphicHex { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsRoadCandidate { get; set; }
    public ApiTileFlagInfo Flags { get; set; } = new ApiTileFlagInfo();
}

/// <summary>
/// Detailed static tile data at one map coordinate.
/// </summary>
public class ApiStaticTileInfo
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public ushort Graphic { get; set; }
    public string GraphicHex { get; set; } = string.Empty;
    public ushort Hue { get; set; }
    public string HueHex { get; set; } = string.Empty;
    public int Height { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsTree { get; set; }
    public bool IsVegetation { get; set; }
    public bool IsCave { get; set; }
    public ApiTileFlagInfo Flags { get; set; } = new ApiTileFlagInfo();
}

/// <summary>
/// Detailed multi component data. HouseSerial is populated when the component belongs to a known house.
/// </summary>
public class ApiMultiComponentInfo
{
    public uint HouseSerial { get; set; }
    public string HouseSerialHex { get; set; } = string.Empty;
    public uint MultiID { get; set; }
    public string MultiIDHex { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public ushort Graphic { get; set; }
    public string GraphicHex { get; set; } = string.Empty;
    public ushort Hue { get; set; }
    public string HueHex { get; set; } = string.Empty;
    public int Height { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MultiOffsetX { get; set; }
    public int MultiOffsetY { get; set; }
    public int MultiOffsetZ { get; set; }
    public bool IsCustom { get; set; }
    public bool IsMovable { get; set; }
    public ApiTileFlagInfo Flags { get; set; } = new ApiTileFlagInfo();
}

/// <summary>
/// Region metadata available to the client for a tile.
/// </summary>
public class ApiRegionInfo
{
    public bool RegionDataAvailable { get; set; }
    public string RegionName { get; set; } = string.Empty;
    public bool IsGuardZone { get; set; }
    public bool NoHousing { get; set; }
    public bool IsTown { get; set; }
    public bool IsDungeon { get; set; }
    public string Source { get; set; } = string.Empty;
}

/// <summary>
/// Full tile inspection payload for one coordinate.
/// </summary>
public class ApiTileInfo
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Map { get; set; }
    public bool InMapBounds { get; set; }
    public bool HasLand { get; set; }
    public ApiLandTileInfo Land { get; set; }
    public List<ApiStaticTileInfo> Statics { get; set; } = new List<ApiStaticTileInfo>();
    public List<ApiMultiComponentInfo> Multis { get; set; } = new List<ApiMultiComponentInfo>();
    public ApiRegionInfo Region { get; set; } = new ApiRegionInfo();
    public bool HasNoHouseFlag { get; set; }
    public bool IsRoadCandidate { get; set; }
    public bool HasImpassable { get; set; }
    public bool HasWet { get; set; }
    public bool HasSurfaceStatic { get; set; }
    public bool HasBridgeStatic { get; set; }
    public bool HasWallOrDoor { get; set; }
}
