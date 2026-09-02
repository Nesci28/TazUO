using System.Text.Json;

namespace ClassicUO.Tools.HDAssets;

internal static class PipelinePlanner
{
    public static void Write(string workDirectory)
    {
        PipelineManifest manifest = PipelineManifest.Load(Path.Combine(workDirectory, "manifest.json"));
        var summary = new PipelineSummary
        {
            Version = manifest.Version,
            Scale = manifest.Scale,
            SheetSize = manifest.SheetSize,
            Padding = manifest.Padding,
            Categories = manifest.Categories,
            MaxAssets = manifest.MaxAssets,
            AssetCount = manifest.Assets.Count,
            Sheets = manifest.Assets
                .GroupBy(x => x.Sheet)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(x => x.Category)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x)
                        .ToArray()
                )
        };
        string path = Path.Combine(workDirectory, "pipeline-summary.json");
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(
                summary,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                }
            )
        );
        Console.WriteLine($"Pipeline summary: {path}");
    }
}

internal sealed class PipelineSummary
{
    public int Version { get; set; }
    public int Scale { get; set; }
    public int SheetSize { get; set; }
    public int Padding { get; set; }
    public string[] Categories { get; set; }
    public int MaxAssets { get; set; }
    public int AssetCount { get; set; }
    public Dictionary<string, string[]> Sheets { get; set; }
}
