using System;
using System.Collections.Generic;
using System.Linq;
using ClassicUO.Assets;
using ClassicUO.Game.GameObjects;
using ClassicUO.LegionScripting.ApiClasses;

namespace ClassicUO.LegionScripting
{
    public partial class LegionAPI
    {
        private static readonly IReadOnlyList<RunUoFoundationCatalogEntry> _runUoCustomHouseCatalog =
            BuildRunUoCustomHouseCatalog();
        private List<RunUoFoundationDefinition> _runUoFoundationDefinitions;

        /// <summary>
        /// Return the exact customizable-foundation size, story, target-offset, and multi-ID catalog
        /// declared by RunUO's HousePlacementTool.
        /// </summary>
        public List<ApiCustomHouseSize> GetCustomHouseFoundationCatalog() =>
            _runUoCustomHouseCatalog.Select(entry => new ApiCustomHouseSize
            {
                Width = entry.Width,
                Depth = entry.Depth,
                Stories = entry.Stories,
                MultiID = entry.MultiID,
                MultiIDHex = $"0x{entry.MultiID:X4}",
                TargetOffsetX = 0,
                TargetOffsetY = entry.TargetOffsetY,
                TargetOffsetZ = 0
            }).ToList();

        /// <summary>
        /// Scan every placement-tool target tile in a square around a coordinate and return the
        /// largest RunUO customizable foundation that fits at each target. The scan uses one
        /// main-thread call and caches map tiles, multi geometry, and known house bounds so it is
        /// suitable for a script that refreshes while the player moves.
        /// </summary>
        /// <param name="centerX">World X at the center of the target-tile scan.</param>
        /// <param name="centerY">World Y at the center of the target-tile scan.</param>
        /// <param name="searchRadius">Target tiles checked in each direction.</param>
        /// <param name="includeTwoStory">Include RunUO's 47 two-story foundations.</param>
        /// <param name="includeThreeStory">Include RunUO's 55 three-story foundations.</param>
        /// <param name="maxTargets">Safety limit for target tiles checked.</param>
        public ApiCustomHouseScanResult ScanCustomHousePlacements(
            int centerX,
            int centerY,
            int searchRadius = 12,
            bool includeTwoStory = true,
            bool includeThreeStory = true,
            int maxTargets = 4096) => OnMain(() => ScanCustomHousePlacementsCore(
                centerX,
                centerY,
                searchRadius,
                includeTwoStory,
                includeThreeStory,
                maxTargets));

        private ApiCustomHouseScanResult ScanCustomHousePlacementsCore(
            int centerX,
            int centerY,
            int searchRadius,
            bool includeTwoStory,
            bool includeThreeStory,
            int maxTargets)
        {
            int safeRadius = Math.Max(0, searchRadius);
            int safeMaxTargets = Math.Max(1, maxTargets);
            var result = new ApiCustomHouseScanResult
            {
                CenterX = centerX,
                CenterY = centerY,
                SearchRadius = safeRadius,
                Map = World?.Map?.Index ?? -1
            };

            AddRunUoScanUncheckedRules(result);

            if (World?.Map == null)
            {
                result.Reason = "Map is not available.";
                return result;
            }

            if (!includeTwoStory && !includeThreeStory)
            {
                result.Reason = "At least one foundation story category must be enabled.";
                return result;
            }

            _runUoFoundationDefinitions ??= _runUoCustomHouseCatalog
                .Select(BuildRunUoFoundationDefinition)
                .Where(definition => definition != null)
                .ToList();

            List<RunUoFoundationDefinition> definitions = _runUoFoundationDefinitions
                .Where(definition => definition.Catalog.Stories == 2 ? includeTwoStory : includeThreeStory)
                .OrderByDescending(definition => definition.Catalog.Area)
                .ThenByDescending(definition => definition.Catalog.Width)
                .ThenByDescending(definition => definition.Catalog.Depth)
                .ToList();
            int expectedDefinitions = _runUoCustomHouseCatalog.Count(entry =>
                entry.Stories == 2 ? includeTwoStory : includeThreeStory);

            if (definitions.Count != expectedDefinitions)
            {
                result.Reason = $"Loaded {definitions.Count} of {expectedDefinitions} custom house multi definitions; scan aborted to avoid incomplete results.";
                return result;
            }

            var context = new RunUoPlacementContext(this);
            bool targetLimitReached = false;

            for (int targetX = centerX - safeRadius; targetX <= centerX + safeRadius && !targetLimitReached; targetX++)
            {
                for (int targetY = centerY - safeRadius; targetY <= centerY + safeRadius; targetY++)
                {
                    if (result.CandidateTargets >= safeMaxTargets)
                    {
                        targetLimitReached = true;
                        break;
                    }

                    result.CandidateTargets++;
                    RunUoPlacementTile targetTile = context.GetTile(targetX, targetY);

                    if (!targetTile.HasLand)
                        continue;

                    int targetZ = targetTile.LandZ;

                    foreach (RunUoFoundationDefinition definition in definitions)
                    {
                        result.TestedPlacements++;

                        if (!context.CanPlace(definition, targetX, targetY, targetZ))
                            continue;

                        ApiCustomHousePlacement placement = BuildCustomHousePlacement(
                            definition,
                            centerX,
                            centerY,
                            targetX,
                            targetY,
                            targetZ);

                        result.Placements.Add(placement);

                        if (IsBetterCustomHousePlacement(placement, result.Best))
                            result.Best = placement;

                        break;
                    }
                }
            }

            result.Placements = result.Placements
                .OrderBy(placement => placement.DistanceSquared)
                .ThenBy(placement => placement.TargetX)
                .ThenBy(placement => placement.TargetY)
                .ToList();
            result.ValidTargets = result.Placements.Count;
            result.Ok = true;
            result.Reason = result.ValidTargets > 0
                ? $"Found {result.ValidTargets} valid placement target(s)."
                : "No valid customizable-house placement target was found.";

            if (targetLimitReached)
                result.Reason += $" Scan stopped at maxTargets={safeMaxTargets}.";

            return result;
        }

        private RunUoFoundationDefinition BuildRunUoFoundationDefinition(RunUoFoundationCatalogEntry catalog)
        {
            try
            {
                List<MultiInfo> multiTiles = Client.Game.UO.FileManager.Multis.GetMultis(catalog.MultiID);

                if (multiTiles == null || multiTiles.Count == 0)
                    return null;

                int minX = multiTiles.Min(tile => (int)tile.X);
                int maxX = multiTiles.Max(tile => (int)tile.X);
                int minY = multiTiles.Min(tile => (int)tile.Y);
                int maxY = multiTiles.Max(tile => (int)tile.Y) + 1;

                if (maxX - minX + 1 != catalog.Width || maxY - minY + 1 != catalog.Depth)
                    return null;

                var groupedTiles = new Dictionary<long, List<RunUoFoundationComponent>>();

                foreach (MultiInfo multiTile in multiTiles)
                    AddRunUoFoundationComponent(groupedTiles, multiTile.ID, multiTile.X, multiTile.Y, multiTile.Z);

                // RunUO HousePlacement.Check calls HouseFoundation.AddStairsTo for these multis.
                for (int x = minX; x <= maxX; x++)
                    AddRunUoFoundationComponent(groupedTiles, 0x0063, x, maxY, 0);

                var definition = new RunUoFoundationDefinition(catalog, minX, minY, maxX, maxY, groupedTiles);
                BuildRunUoFoundationClearance(definition);
                return definition;
            }
            catch
            {
                return null;
            }
        }

        private static void AddRunUoFoundationComponent(
            Dictionary<long, List<RunUoFoundationComponent>> groupedTiles,
            ushort graphic,
            int x,
            int y,
            int z)
        {
            StaticTiles itemData = Client.Game.UO.FileManager.TileData.StaticData[graphic];
            long key = GetCoordinateKey(x, y);

            if (!groupedTiles.TryGetValue(key, out List<RunUoFoundationComponent> components))
            {
                components = new List<RunUoFoundationComponent>();
                groupedTiles[key] = components;
            }

            components.Add(new RunUoFoundationComponent(
                graphic,
                z,
                itemData.Height,
                itemData.Flags));
        }

        private static void BuildRunUoFoundationClearance(RunUoFoundationDefinition definition)
        {
            var yard = new HashSet<long>();
            var borders = new HashSet<long>();

            foreach ((long key, List<RunUoFoundationComponent> components) in definition.GroupedTiles)
            {
                if (!components.Any(component => component.IsFoundation))
                    continue;

                DecodeCoordinateKey(key, out int x, out int y);

                for (int xOffset = -1; xOffset <= 1; xOffset++)
                {
                    for (int yOffset = -5; yOffset <= 5; yOffset++)
                        yard.Add(GetCoordinateKey(x + xOffset, y + yOffset));
                }

                for (int xOffset = -1; xOffset <= 1; xOffset++)
                {
                    for (int yOffset = -1; yOffset <= 1; yOffset++)
                    {
                        if (xOffset == 0 && yOffset == 0)
                            continue;

                        long neighborKey = GetCoordinateKey(x + xOffset, y + yOffset);

                        if (definition.GroupedTiles.TryGetValue(neighborKey, out List<RunUoFoundationComponent> neighborTiles) &&
                            neighborTiles.Any(tile => tile.Height == 0 && tile.Z <= 8 && tile.IsSurface))
                        {
                            continue;
                        }

                        borders.Add(neighborKey);
                    }
                }
            }

            definition.YardOffsets = yard.ToArray();
            definition.BorderOffsets = borders.ToArray();
        }

        private static ApiCustomHousePlacement BuildCustomHousePlacement(
            RunUoFoundationDefinition definition,
            int scanCenterX,
            int scanCenterY,
            int targetX,
            int targetY,
            int targetZ)
        {
            int centerX = targetX;
            int centerY = targetY - definition.Catalog.TargetOffsetY;
            long dx = (long)targetX - scanCenterX;
            long dy = (long)targetY - scanCenterY;

            return new ApiCustomHousePlacement
            {
                TargetX = targetX,
                TargetY = targetY,
                TargetZ = targetZ,
                CenterX = centerX,
                CenterY = centerY,
                CenterZ = targetZ,
                X = centerX + definition.MinX,
                Y = centerY + definition.MinY,
                MinX = centerX + definition.MinX,
                MinY = centerY + definition.MinY,
                MaxX = centerX + definition.MaxX,
                MaxY = centerY + definition.MaxY,
                Width = definition.Catalog.Width,
                Depth = definition.Catalog.Depth,
                Stories = definition.Catalog.Stories,
                Area = definition.Catalog.Area,
                MultiID = definition.Catalog.MultiID,
                MultiIDHex = $"0x{definition.Catalog.MultiID:X4}",
                DistanceSquared = dx * dx + dy * dy
            };
        }

        private static bool IsBetterCustomHousePlacement(ApiCustomHousePlacement candidate, ApiCustomHousePlacement current)
        {
            if (current == null)
                return true;

            if (candidate.Area != current.Area)
                return candidate.Area > current.Area;

            if (candidate.DistanceSquared != current.DistanceSquared)
                return candidate.DistanceSquared < current.DistanceSquared;

            if (candidate.Width != current.Width)
                return candidate.Width > current.Width;

            if (candidate.TargetX != current.TargetX)
                return candidate.TargetX < current.TargetX;

            return candidate.TargetY < current.TargetY;
        }

        private static void AddRunUoScanUncheckedRules(ApiCustomHouseScanResult result)
        {
            result.UncheckedServerSideRules.Add("AllowHousing region checks, temporary no-housing regions, raffles, and shard-specific map restrictions are not available to the client.");
            result.UncheckedServerSideRules.Add("Account ownership, housing cooldown, character state, and bank balance are checked only by the shard server.");
            result.UncheckedServerSideRules.Add("Movable items and mobiles are accepted because RunUO relocates them after placement; the server remains authoritative.");
        }

        internal static IReadOnlyList<RunUoFoundationCatalogEntry> BuildRunUoCustomHouseCatalog()
        {
            var entries = new List<RunUoFoundationCatalogEntry>(102);

            AddCatalogRange(entries, 2, 7, 7, 12, 0x13EC);
            AddCatalogRange(entries, 2, 8, 7, 13, 0x13F8);
            AddCatalogRange(entries, 2, 9, 7, 13, 0x1404);
            AddCatalogRange(entries, 2, 10, 7, 13, 0x1410);
            AddCatalogRange(entries, 2, 11, 7, 13, 0x141C);
            AddCatalogRange(entries, 2, 12, 7, 13, 0x1428);
            AddCatalogRange(entries, 2, 13, 8, 13, 0x1435);

            AddCatalogRange(entries, 3, 9, 14, 14, 0x140B);
            AddCatalogRange(entries, 3, 10, 14, 15, 0x1417);
            AddCatalogRange(entries, 3, 11, 14, 16, 0x1423);
            AddCatalogRange(entries, 3, 12, 14, 17, 0x142F);
            AddCatalogRange(entries, 3, 13, 14, 18, 0x143B);
            AddCatalogRange(entries, 3, 14, 9, 18, 0x1442);
            AddCatalogRange(entries, 3, 15, 10, 18, 0x144F);
            AddCatalogRange(entries, 3, 16, 11, 18, 0x145C);
            AddCatalogRange(entries, 3, 17, 12, 18, 0x1469);
            AddCatalogRange(entries, 3, 18, 13, 18, 0x1476);

            return entries;
        }

        private static void AddCatalogRange(
            List<RunUoFoundationCatalogEntry> entries,
            int stories,
            int width,
            int minimumDepth,
            int maximumDepth,
            uint firstMultiID)
        {
            for (int depth = minimumDepth; depth <= maximumDepth; depth++)
            {
                entries.Add(new RunUoFoundationCatalogEntry(
                    width,
                    depth,
                    stories,
                    firstMultiID + (uint)(depth - minimumDepth),
                    depth / 2 + 1));
            }
        }

        private static long GetCoordinateKey(int x, int y) => ((long)x << 32) | (uint)y;

        private static void DecodeCoordinateKey(long key, out int x, out int y)
        {
            x = (int)(key >> 32);
            y = (int)key;
        }

        internal readonly struct RunUoFoundationCatalogEntry
        {
            public RunUoFoundationCatalogEntry(int width, int depth, int stories, uint multiID, int targetOffsetY)
            {
                Width = width;
                Depth = depth;
                Stories = stories;
                MultiID = multiID;
                TargetOffsetY = targetOffsetY;
            }

            public int Width { get; }
            public int Depth { get; }
            public int Stories { get; }
            public uint MultiID { get; }
            public int TargetOffsetY { get; }
            public int Area => Width * Depth;
        }

        private sealed class RunUoFoundationDefinition
        {
            public RunUoFoundationDefinition(
                RunUoFoundationCatalogEntry catalog,
                int minX,
                int minY,
                int maxX,
                int maxY,
                Dictionary<long, List<RunUoFoundationComponent>> groupedTiles)
            {
                Catalog = catalog;
                MinX = minX;
                MinY = minY;
                MaxX = maxX;
                MaxY = maxY;
                GroupedTiles = groupedTiles;
            }

            public RunUoFoundationCatalogEntry Catalog { get; }
            public int MinX { get; }
            public int MinY { get; }
            public int MaxX { get; }
            public int MaxY { get; }
            public Dictionary<long, List<RunUoFoundationComponent>> GroupedTiles { get; }
            public long[] BorderOffsets { get; set; } = Array.Empty<long>();
            public long[] YardOffsets { get; set; } = Array.Empty<long>();
        }

        private readonly struct RunUoFoundationComponent
        {
            public RunUoFoundationComponent(ushort graphic, int z, int height, TileFlag flags)
            {
                Graphic = graphic;
                Z = z;
                Height = height;
                Flags = flags;
            }

            public ushort Graphic { get; }
            public int Z { get; }
            public int Height { get; }
            public TileFlag Flags { get; }
            public bool IsNodraw => Graphic == 0x0001;
            public bool IsFoundation => Z == 0 && (Flags & TileFlag.Wall) != 0;
            public bool IsSurface => (Flags & TileFlag.Surface) != 0;
            public int TopOffset => Z + Height + (IsSurface ? 16 : 0);
        }

        private sealed class RunUoPlacementContext
        {
            private readonly LegionAPI _api;
            private readonly Dictionary<long, RunUoPlacementTile> _tiles = new();
            private readonly List<RunUoHouseBounds> _houseBounds = new();

            public RunUoPlacementContext(LegionAPI api)
            {
                _api = api;
                BuildHouseBounds();
            }

            public RunUoPlacementTile GetTile(int x, int y)
            {
                long key = GetCoordinateKey(x, y);

                if (_tiles.TryGetValue(key, out RunUoPlacementTile cached))
                    return cached;

                RunUoPlacementTile tile = BuildTile(x, y);
                _tiles[key] = tile;
                return tile;
            }

            public bool CanPlace(RunUoFoundationDefinition definition, int targetX, int targetY, int centerZ)
            {
                int centerX = targetX;
                int centerY = targetY - definition.Catalog.TargetOffsetY;

                foreach ((long key, List<RunUoFoundationComponent> components) in definition.GroupedTiles)
                {
                    DecodeCoordinateKey(key, out int offsetX, out int offsetY);
                    RunUoPlacementTile tile = GetTile(centerX + offsetX, centerY + offsetY);

                    if (!tile.HasLand || tile.ClientNoHousing || IsRunUoRoadGraphic(tile.LandGraphic))
                        return false;

                    foreach (RunUoFoundationComponent component in components)
                    {
                        if (component.IsNodraw)
                            continue;

                        int componentZ = centerZ + component.Z;
                        int componentTop = centerZ + component.TopOffset;

                        if (componentTop > tile.LandZ && tile.LandAverageZ > componentZ)
                            return false;

                        if (tile.BlocksComponent(componentZ, componentTop))
                            return false;

                        if (component.IsFoundation &&
                            (((tile.LandFlags & TileFlag.Impassable) != 0) || tile.LandAverageZ != centerZ))
                        {
                            return false;
                        }
                    }
                }

                foreach (long key in definition.BorderOffsets)
                {
                    DecodeCoordinateKey(key, out int offsetX, out int offsetY);
                    RunUoPlacementTile tile = GetTile(centerX + offsetX, centerY + offsetY);

                    if (!tile.HasLand ||
                        (tile.LandFlags & TileFlag.Impassable) != 0 ||
                        IsRunUoRoadGraphic(tile.LandGraphic) ||
                        tile.BlocksBorder(centerZ + 2))
                    {
                        return false;
                    }
                }

                foreach (long key in definition.YardOffsets)
                {
                    DecodeCoordinateKey(key, out int offsetX, out int offsetY);
                    int x = centerX + offsetX;
                    int y = centerY + offsetY;

                    foreach (RunUoHouseBounds bounds in _houseBounds)
                    {
                        if (bounds.Contains(x, y))
                            return false;
                    }
                }

                return true;
            }

            private RunUoPlacementTile BuildTile(int x, int y)
            {
                var tile = new RunUoPlacementTile();

                if (!_api.IsInCurrentMapBounds(x, y) || _api.World?.Map == null)
                    return tile;

                GameObject first = _api.World.Map.GetTile(x, y, true);
                Land land = first as Land;

                if (land == null)
                    return tile;

                tile.HasLand = true;
                tile.LandGraphic = land.OriginalGraphic;
                tile.LandZ = land.Z;
                tile.LandAverageZ = land.AverageZ;
                tile.LandFlags = land.TileData.Flags;
                tile.ClientNoHousing = (tile.LandFlags & TileFlag.NoHouse) != 0;

                for (GameObject gameObject = first; gameObject != null; gameObject = gameObject.TNext)
                {
                    switch (gameObject)
                    {
                        case Static staticObject:
                            tile.AddObstacle(staticObject.Z, staticObject.ItemData.Height, staticObject.ItemData.Flags, false);
                            tile.ClientNoHousing |= (staticObject.ItemData.Flags & TileFlag.NoHouse) != 0;
                            break;
                        case Multi multi:
                            tile.AddObstacle(multi.Z, multi.ItemData.Height, multi.ItemData.Flags, false);
                            break;
                        case Item item:
                            tile.AddObstacle(item.Z, item.ItemData.Height, item.ItemData.Flags, item.IsMovable);
                            tile.ClientNoHousing |= (item.ItemData.Flags & TileFlag.NoHouse) != 0;
                            break;
                    }
                }

                return tile;
            }

            private void BuildHouseBounds()
            {
                if (_api.World?.HouseManager == null)
                    return;

                foreach (House house in _api.World.HouseManager.Houses)
                {
                    int minX = int.MaxValue;
                    int minY = int.MaxValue;
                    int maxX = int.MinValue;
                    int maxY = int.MinValue;

                    foreach (Multi component in house.Components)
                    {
                        if (component == null || component.IsDestroyed)
                            continue;

                        minX = Math.Min(minX, component.X);
                        minY = Math.Min(minY, component.Y);
                        maxX = Math.Max(maxX, component.X);
                        maxY = Math.Max(maxY, component.Y);
                    }

                    if (minX == int.MaxValue)
                    {
                        Item foundation = _api.World.Items?.Get(house.Serial);

                        if (foundation == null)
                            continue;

                        minX = foundation.X + house.Bounds.X;
                        minY = foundation.Y + house.Bounds.Y;
                        maxX = foundation.X + house.Bounds.Width;
                        maxY = foundation.Y + house.Bounds.Height;
                    }

                    _houseBounds.Add(new RunUoHouseBounds(minX, minY, maxX, maxY));
                }
            }
        }

        private sealed class RunUoPlacementTile
        {
            private readonly List<RunUoObstacle> _obstacles = new();

            public bool HasLand { get; set; }
            public ushort LandGraphic { get; set; }
            public int LandZ { get; set; }
            public int LandAverageZ { get; set; }
            public TileFlag LandFlags { get; set; }
            public bool ClientNoHousing { get; set; }

            public void AddObstacle(int z, int height, TileFlag flags, bool movable) =>
                _obstacles.Add(new RunUoObstacle(z, height, flags, movable));

            public bool BlocksComponent(int componentZ, int componentTop)
            {
                foreach (RunUoObstacle obstacle in _obstacles)
                {
                    if (obstacle.Movable || !obstacle.BlocksPlacement)
                        continue;

                    if (componentTop > obstacle.Z && obstacle.Top > componentZ)
                        return true;
                }

                return false;
            }

            public bool BlocksBorder(int minimumSurfaceTop)
            {
                foreach (RunUoObstacle obstacle in _obstacles)
                {
                    if (obstacle.Movable)
                        continue;

                    if (obstacle.IsImpassable || (obstacle.IsSurface && !obstacle.IsBackground && obstacle.Top > minimumSurfaceTop))
                        return true;
                }

                return false;
            }
        }

        private readonly struct RunUoObstacle
        {
            public RunUoObstacle(int z, int height, TileFlag flags, bool movable)
            {
                Z = z;
                Height = height;
                Flags = flags;
                Movable = movable;
            }

            public int Z { get; }
            public int Height { get; }
            public TileFlag Flags { get; }
            public bool Movable { get; }
            public int Top => Z + Height;
            public bool IsImpassable => (Flags & TileFlag.Impassable) != 0;
            public bool IsSurface => (Flags & TileFlag.Surface) != 0;
            public bool IsBackground => (Flags & TileFlag.Background) != 0;
            public bool BlocksPlacement => IsImpassable || (IsSurface && !IsBackground);
        }

        private readonly struct RunUoHouseBounds
        {
            public RunUoHouseBounds(int minX, int minY, int maxX, int maxY)
            {
                MinX = minX;
                MinY = minY;
                MaxX = maxX;
                MaxY = maxY;
            }

            public int MinX { get; }
            public int MinY { get; }
            public int MaxX { get; }
            public int MaxY { get; }
            public bool Contains(int x, int y) => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
        }
    }
}
