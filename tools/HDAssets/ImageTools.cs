using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ClassicUO.Tools.HDAssets;

internal static class ImageTools
{
    public static Rgba32[] ConvertPixels(ReadOnlySpan<uint> pixels, int width, int height)
    {
        var result = new Rgba32[width * height];

        for (int i = 0; i < result.Length; i++)
        {
            uint pixel = pixels[i];
            result[i] = new Rgba32(
                (byte)pixel,
                (byte)(pixel >> 8),
                (byte)(pixel >> 16),
                (byte)(pixel >> 24)
            );
        }

        return result;
    }

    public static void Save(string path, Rgba32[] pixels, int width, int height)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var image = new Image<Rgba32>(width, height);
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < height; y++)
                pixels.AsSpan(y * width, width).CopyTo(accessor.GetRowSpan(y));
        });
        image.SaveAsPng(path);
    }

    public static Rgba32[] CreateOpaqueAiInput(Rgba32[] source, int width, int height)
    {
        var result = (Rgba32[])source.Clone();
        var visited = new bool[result.Length];
        var queue = new Queue<int>(result.Length);

        for (int i = 0; i < result.Length; i++)
        {
            if (result[i].A == 0)
                continue;

            result[i].A = byte.MaxValue;
            visited[i] = true;
            queue.Enqueue(i);
        }

        if (queue.Count == 0)
            return null;

        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            int x = index % width;
            int y = index / width;

            Visit(x - 1, y, index);
            Visit(x + 1, y, index);
            Visit(x, y - 1, index);
            Visit(x, y + 1, index);
        }

        return result;

        void Visit(int x, int y, int sourceIndex)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;

            int targetIndex = y * width + x;
            if (visited[targetIndex])
                return;

            Rgba32 color = result[sourceIndex];
            color.A = byte.MaxValue;
            result[targetIndex] = color;
            visited[targetIndex] = true;
            queue.Enqueue(targetIndex);
        }
    }

    public static string AddScaleSuffix(string relativePath, int scale)
    {
        string extension = Path.GetExtension(relativePath);
        return relativePath[..^extension.Length] + $"@{scale}x" + extension;
    }
}
