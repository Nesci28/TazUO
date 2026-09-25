using System.Text.Json;

namespace ClassicUO.Tools.HDAssets;

internal sealed class PipelineManifest
{
    public int Version { get; set; } = 1;
    public int Scale { get; set; }
    public int SheetSize { get; set; }
    public int Padding { get; set; }
    public string CreatedUtc { get; set; } = DateTime.UtcNow.ToString("O");
    public string UoDirectory { get; set; }
    public string[] Categories { get; set; } = Array.Empty<string>();
    public int MaxAssets { get; set; }
    public List<AssetEntry> Assets { get; set; } = new List<AssetEntry>();
    public Dictionary<string, int> ExportedByCategory { get; set; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> SkippedByReason { get; set; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public static PipelineManifest Load(string path) =>
        JsonSerializer.Deserialize<PipelineManifest>(File.ReadAllText(path), JsonOptions)
        ?? throw new InvalidDataException($"Invalid pipeline manifest: {path}");

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
}

internal sealed class AssetEntry
{
    public string Category { get; set; }
    public string Key { get; set; }
    public string SourcePath { get; set; }
    public string OutputPath { get; set; }
    public string Sheet { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool RestoreGrayscaleMask { get; set; }
}
