using System.Text.Json;
using ClassicUO.Assets;
using ClassicUO.IO;
using ClassicUO.Utility;

namespace ClassicUO.Tools.HDAssets;

internal static class SceneExporter
{
    public static void Run(string uoDirectory, string outputPath, int map, int originX, int originY, int width, int height)
    {
        if (width is < 1 or > 256 || height is < 1 or > 256)
            throw new ArgumentOutOfRangeException(nameof(width), "Scene dimensions must be between 1 and 256.");

        using var files = new UOFileManager(ClientVersion.CV_7010400, uoDirectory);
        files.Load(useVerdata: false, lang: "enu", graphicsDevice: null);

        if (map < 0 || map >= MapLoader.MAPS_COUNT)
            throw new ArgumentOutOfRangeException(nameof(map));

        files.Maps.LoadMap(map);

        var cells = new SceneCell[width * height];
        var statics = new List<SceneStatic>();
        var ids = new HashSet<ushort>();
        FileReader mapFile = files.Maps.GetMapFile(map);
        if (mapFile == null)
            throw new FileNotFoundException($"Map {map} is not available.");

        int index = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int worldX = originX + x;
                int worldY = originY + y;
                ref IndexMap block = ref files.Maps.GetIndex(map, worldX >> 3, worldY >> 3);
                if (!block.IsValid())
                    continue;

                MapBlock mapBlock = block.MapFile.ReadAt<MapBlock>((long)block.MapAddress);
                MapCells cell = mapBlock.Cells[(worldX & 7) * 8 + (worldY & 7)];
                cells[index] = new SceneCell(worldX, worldY, cell.TileID, cell.Z);
                ids.Add(cell.TileID);
                index++;

                if (block.StaticFile != null && block.StaticCount > 0)
                {
                    for (uint staticIndex = 0; staticIndex < block.StaticCount; staticIndex++)
                    {
                        StaticsBlock item = block.StaticFile.ReadAt<StaticsBlock>(
                            (long)(block.StaticAddress + staticIndex * (uint)System.Runtime.InteropServices.Marshal.SizeOf<StaticsBlock>()));
                        if (item.X == (worldX & 7) && item.Y == (worldY & 7))
                            statics.Add(new SceneStatic(worldX, worldY, item.Color, item.Z, item.Hue));
                    }
                }
            }
        }

        var scene = new SceneDocument(map, originX, originY, width, height, cells, statics, ids.Order().ToArray());
        string fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(scene, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Wrote {width}x{height} map scene with {ids.Count} land tiles: {fullPath}");
    }

    private sealed record SceneDocument(int Map, int X, int Y, int Width, int Height, SceneCell[] Cells, List<SceneStatic> Statics, ushort[] LandTileIds);
    private sealed record SceneCell(int X, int Y, ushort LandTileId, sbyte Z);
    private sealed record SceneStatic(int X, int Y, ushort ArtId, sbyte Z, ushort Hue);
}
