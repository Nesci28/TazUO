using System.Globalization;
using ClassicUO.Assets;

namespace ClassicUO.Tools.HDAssets;

internal static class HdPackBuilder
{
    public static int Run(string workDirectory, string inputDirectory, string outputPath)
    {
        PipelineManifest manifest = PipelineManifest.Load(
            Path.Combine(workDirectory, "manifest.json")
        );
        string inputRoot = Path.GetFullPath(inputDirectory);
        string inputPrefix = inputRoot.EndsWith(Path.DirectorySeparatorChar)
            ? inputRoot
            : inputRoot + Path.DirectorySeparatorChar;
        var assets = new List<HdAssetPackSource>(manifest.Assets.Count);

        foreach (AssetEntry entry in manifest.Assets)
        {
            string sourcePath = Path.GetFullPath(Path.Combine(inputRoot, entry.OutputPath));
            if (!sourcePath.StartsWith(inputPrefix, StringComparison.Ordinal))
                throw new InvalidDataException($"Output path leaves the asset directory: {entry.OutputPath}");
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Finalized HD asset is missing.", sourcePath);

            (HdAssetKind kind, ulong key) = ParseKey(entry);
            assets.Add(new HdAssetPackSource(kind, key, manifest.Scale, sourcePath));
        }

        Console.WriteLine($"Packing {assets.Count:N0} finalized images without recompression...");
        HdAssetPackWriter.Write(
            outputPath,
            assets,
            (completed, total, bytes) =>
                Console.WriteLine(
                    $"  packed images: {completed:N0}/{total:N0} " +
                    $"({bytes / (1024d * 1024d * 1024d):N2} GiB)"
                )
        );
        long size = new FileInfo(outputPath).Length;
        Console.WriteLine(
            $"HD pack: {Path.GetFullPath(outputPath)} ({size / (1024d * 1024d * 1024d):N2} GiB)"
        );
        return assets.Count;
    }

    private static (HdAssetKind kind, ulong key) ParseKey(AssetEntry entry)
    {
        switch (entry.Category.ToLowerInvariant())
        {
            case "gumps":
                return (HdAssetKind.Gump, ParseUInt(entry.Key));
            case "art":
                return (HdAssetKind.Art, checked(ParseUInt(entry.Key) + 0x4000U));
            case "land":
                return (HdAssetKind.Land, ParseUInt(entry.Key));
            case "texmaps":
                return (HdAssetKind.Texmap, ParseUInt(entry.Key));
            case "animations":
                return (HdAssetKind.Animation, ParseAnimationKey(entry.Key));
            default:
                throw new InvalidDataException($"Unsupported HD asset category: {entry.Category}");
        }
    }

    private static uint ParseUInt(string value)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return uint.Parse(
                value.AsSpan(2),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture
            );
        }
        return uint.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
    }

    private static ulong ParseAnimationKey(string value)
    {
        string[] parts = value.Replace('\\', '/').Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries
        );
        if (parts.Length != 4)
            throw new InvalidDataException($"Invalid animation key: {value}");

        uint body = ParseUInt(parts[0]);
        uint action = ParseUInt(parts[1]);
        uint direction = ParseUInt(parts[2]);
        uint frame = ParseUInt(parts[3]);
        if (
            body > ushort.MaxValue
            || action >= AnimationsLoader.MAX_ACTIONS
            || direction >= AnimationsLoader.MAX_DIRECTIONS
            || frame > ushort.MaxValue
        )
        {
            throw new InvalidDataException($"Animation key is outside the supported range: {value}");
        }

        return body | ((ulong)action << 16) | ((ulong)direction << 24) | ((ulong)frame << 32);
    }
}
