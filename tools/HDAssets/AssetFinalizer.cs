using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ClassicUO.Tools.HDAssets;

internal sealed class AssetFinalizer
{
    private readonly string _workDirectory;
    private readonly string _upscaledDirectory;
    private readonly string _outputDirectory;
    private readonly string _completedSheetsDirectory;
    private readonly bool _deleteUpscaledSheets;
    private readonly PipelineManifest _manifest;

    public AssetFinalizer(
        string workDirectory,
        string upscaledDirectory,
        string outputDirectory,
        bool deleteUpscaledSheets
    )
    {
        _workDirectory = workDirectory;
        _upscaledDirectory = upscaledDirectory;
        _outputDirectory = outputDirectory;
        _manifest = PipelineManifest.Load(Path.Combine(workDirectory, "manifest.json"));
        _completedSheetsDirectory = Path.Combine(
            workDirectory,
            $"finalized-{_manifest.Scale}x"
        );
        _deleteUpscaledSheets = deleteUpscaledSheets;
    }

    public PackReport Run()
    {
        Directory.CreateDirectory(_outputDirectory);
        var report = new PackReport { Expected = _manifest.Assets.Count };
        int completed = 0;

        foreach (IGrouping<string, AssetEntry> sheetGroup in _manifest.Assets.GroupBy(x => x.Sheet))
        {
            List<AssetEntry> entries = sheetGroup.ToList();
            string markerPath = GetCompletedMarkerPath(sheetGroup.Key);
            if (File.Exists(markerPath) && entries.All(OutputExists))
            {
                foreach (AssetEntry entry in entries)
                {
                    RecordWritten(report, entry);
                    completed++;
                }

                if (_deleteUpscaledSheets)
                {
                    string completedSheetPath = FindUpscaledSheet(sheetGroup.Key);
                    if (completedSheetPath != null)
                        File.Delete(completedSheetPath);
                }

                continue;
            }

            if (File.Exists(markerPath))
                File.Delete(markerPath);

            string sheetPath = FindUpscaledSheet(sheetGroup.Key);
            if (sheetPath == null)
            {
                int missingCount = entries.Count;
                report.MissingSheets++;
                report.MissingAssets += missingCount;
                Console.Error.WriteLine($"Missing upscaled sheet: {sheetGroup.Key} ({missingCount:N0} assets)");
                continue;
            }

            using Image<Rgba32> upscaledSheet = Image.Load<Rgba32>(sheetPath);
            var sheetPixels = new Rgba32[upscaledSheet.Width * upscaledSheet.Height];
            upscaledSheet.CopyPixelDataTo(sheetPixels);
            bool sheetSucceeded = true;

            foreach (AssetEntry entry in entries)
            {
                try
                {
                    FinalizeAsset(entry, sheetPixels, upscaledSheet.Width, upscaledSheet.Height);
                    completed++;
                    RecordWritten(report, entry);

                    if (completed % 1000 == 0)
                        Console.WriteLine($"  finalized assets: {completed:N0}/{_manifest.Assets.Count:N0}");
                }
                catch (Exception ex)
                {
                    sheetSucceeded = false;
                    report.Errors++;
                    Console.Error.WriteLine($"Failed {entry.OutputPath}: {ex.Message}");
                }
            }

            if (sheetSucceeded && _deleteUpscaledSheets)
            {
                Directory.CreateDirectory(_completedSheetsDirectory);
                string temporaryMarker = markerPath + ".tmp";
                File.WriteAllText(
                    temporaryMarker,
                    entries.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                );
                File.Move(temporaryMarker, markerPath, true);
                File.Delete(sheetPath);
            }
        }

        string reportPath = Path.Combine(_workDirectory, "pack-report.json");
        File.WriteAllText(
            reportPath,
            JsonSerializer.Serialize(
                report,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                }
            )
        );

        Console.WriteLine($"Finalized {report.Written:N0}/{report.Expected:N0} assets into: {_outputDirectory}");
        Console.WriteLine($"Report: {reportPath}");
        return report;
    }

    private bool OutputExists(AssetEntry entry) =>
        File.Exists(Path.Combine(_outputDirectory, entry.OutputPath));

    private string GetCompletedMarkerPath(string sheetName) =>
        Path.Combine(
            _completedSheetsDirectory,
            Path.GetFileNameWithoutExtension(sheetName) + ".done"
        );

    private static void RecordWritten(PackReport report, AssetEntry entry)
    {
        report.Written++;
        report.WrittenByCategory[entry.Category] =
            report.WrittenByCategory.GetValueOrDefault(entry.Category) + 1;
    }

    private void FinalizeAsset(
        AssetEntry entry,
        Rgba32[] sheetPixels,
        int sheetWidth,
        int sheetHeight
    )
    {
        int scale = _manifest.Scale;
        int outputWidth = entry.Width * scale;
        int outputHeight = entry.Height * scale;
        int sheetX = entry.X * scale;
        int sheetY = entry.Y * scale;

        if (sheetX < 0 || sheetY < 0 || sheetX + outputWidth > sheetWidth || sheetY + outputHeight > sheetHeight)
        {
            throw new InvalidDataException(
                $"Upscaled sheet is {sheetWidth}x{sheetHeight}; crop requires " +
                $"{sheetX},{sheetY} {outputWidth}x{outputHeight}."
            );
        }

        string sourcePath = Path.Combine(_workDirectory, "source", entry.SourcePath);
        using Image<Rgba32> source = Image.Load<Rgba32>(sourcePath);
        if (source.Width != entry.Width || source.Height != entry.Height)
            throw new InvalidDataException("Source dimensions differ from the manifest.");

        var sourcePixels = new Rgba32[source.Width * source.Height];
        source.CopyPixelDataTo(sourcePixels);
        var outputPixels = new Rgba32[outputWidth * outputHeight];

        for (int y = 0; y < outputHeight; y++)
        {
            int originalY = y / scale;
            int sourceSheetOffset = (sheetY + y) * sheetWidth + sheetX;
            int outputOffset = y * outputWidth;

            for (int x = 0; x < outputWidth; x++)
            {
                Rgba32 original = sourcePixels[originalY * entry.Width + x / scale];
                if (original.A == 0)
                    continue;

                Rgba32 pixel = sheetPixels[sourceSheetOffset + x];
                pixel.A = byte.MaxValue;

                if (
                    entry.RestoreGrayscaleMask
                    && original.R == original.G
                    && original.R == original.B
                )
                {
                    byte gray = (byte)((pixel.R + pixel.G + pixel.B) / 3);
                    pixel.R = gray;
                    pixel.G = gray;
                    pixel.B = gray;
                }

                outputPixels[outputOffset + x] = pixel;
            }
        }

        ImageTools.Save(
            Path.Combine(_outputDirectory, entry.OutputPath),
            outputPixels,
            outputWidth,
            outputHeight
        );
    }

    private string FindUpscaledSheet(string sheetName)
    {
        string exactPath = Path.Combine(_upscaledDirectory, sheetName);
        if (File.Exists(exactPath))
            return exactPath;

        string stem = Path.GetFileNameWithoutExtension(sheetName);
        string[] matches = Directory.Exists(_upscaledDirectory)
            ? Directory.GetFiles(_upscaledDirectory, stem + "*.png", SearchOption.TopDirectoryOnly)
            : Array.Empty<string>();
        return matches.Length == 1 ? matches[0] : null;
    }
}

internal sealed class PackReport
{
    public int Expected { get; set; }
    public int Written { get; set; }
    public int MissingSheets { get; set; }
    public int MissingAssets { get; set; }
    public int Errors { get; set; }
    public Dictionary<string, int> WrittenByCategory { get; set; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
}
