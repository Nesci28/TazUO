using SixLabors.ImageSharp;

namespace ClassicUO.Tools.HDAssets;

internal static class PackValidator
{
    public static bool Run(string workDirectory, string outputDirectory)
    {
        PipelineManifest manifest = PipelineManifest.Load(Path.Combine(workDirectory, "manifest.json"));
        int missing = 0;
        int invalid = 0;
        int checkedCount = 0;

        foreach (AssetEntry entry in manifest.Assets)
        {
            string path = Path.Combine(outputDirectory, entry.OutputPath);
            if (!File.Exists(path))
            {
                missing++;
                continue;
            }

            var info = Image.Identify(path);
            int expectedWidth = entry.Width * manifest.Scale;
            int expectedHeight = entry.Height * manifest.Scale;
            if (info == null || info.Width != expectedWidth || info.Height != expectedHeight)
            {
                invalid++;
                Console.Error.WriteLine(
                    $"Invalid dimensions: {entry.OutputPath}; expected {expectedWidth}x{expectedHeight}, " +
                    $"got {(info == null ? "unreadable" : $"{info.Width}x{info.Height}")}"
                );
            }

            checkedCount++;
            if (checkedCount % 5000 == 0)
                Console.WriteLine($"  validated assets: {checkedCount:N0}/{manifest.Assets.Count:N0}");
        }

        Console.WriteLine(
            $"Validation: {checkedCount:N0} present, {missing:N0} missing, {invalid:N0} invalid."
        );
        return missing == 0 && invalid == 0;
    }
}
