using ClassicUO.Assets;
using ClassicUO.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ClassicUO.Tools.HDAssets;

internal static class AnimationExporter
{
    public static void Run(string uoDirectory, string outputPath, ushort body, int group, int direction)
    {
        using var files = new UOFileManager(ClientVersion.CV_7010400, uoDirectory);
        files.Load(useVerdata: false, lang: "enu", graphicsDevice: null);

        ushort hue = 0;
        AnimationFlags flags = AnimationFlags.None;
        ReadOnlySpan<AnimationsLoader.AnimationDirection> indices = files.Animations.GetIndices(
            files.Version, body, ref hue, ref flags, out int fileIndex, out AnimationGroupsType type);
        if (group < 0 || group >= indices.Length)
            throw new ArgumentOutOfRangeException(nameof(group));

        var frames = (flags & AnimationFlags.UseUopAnimation) != 0
            ? files.Animations.ReadUOPAnimationFrames(body, (byte)group, (byte)direction, type, fileIndex, indices[group])
            : files.Animations.ReadMULAnimationFrames(fileIndex, indices[group]);
        if (frames.IsEmpty || frames[0].Width <= 0 || frames[0].Height <= 0)
            throw new InvalidOperationException($"No animation frame found for body {body}, group {group}, direction {direction}.");

        ref readonly AnimationsLoader.FrameInfo frame = ref frames[0];
        int frameWidth = frame.Width;
        int frameHeight = frame.Height;
        uint[] pixels = frame.Pixels;
        using var image = new Image<Rgba32>(frameWidth, frameHeight);
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < frameHeight; y++)
            {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                for (int x = 0; x < frameWidth; x++)
                {
                    uint pixel = pixels[y * frameWidth + x];
                    row[x] = new Rgba32((byte)(pixel >> 16), (byte)(pixel >> 8), (byte)pixel, pixel == 0 ? (byte)0 : (byte)255);
                }
            }
        });

        string fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        image.SaveAsPng(fullPath);
        Console.WriteLine($"Wrote body {body} frame {frame.Width}x{frame.Height}: {fullPath}");
    }
}
