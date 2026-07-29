namespace ClassicUO.Tools.HDAssets;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
            {
                PrintHelp();
                return 0;
            }

            var options = ParseOptions(args.Skip(1));

            switch (args[0].ToLowerInvariant())
            {
                case "export":
                    RunExport(options);
                    return 0;
                case "finalize":
                    return RunFinalize(options);
                case "validate":
                    return RunValidate(options);
                case "pack":
                    HdPackBuilder.Run(
                        Require(options, "work"),
                        Require(options, "input"),
                        Require(options, "output")
                    );
                    return 0;
                case "plan":
                    PipelinePlanner.Write(Require(options, "work"));
                    return 0;
                default:
                    throw new ArgumentException($"Unknown command: {args[0]}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void RunExport(Dictionary<string, string> options)
    {
        string categoriesValue = Get(options, "categories", "land,art,gumps,texmaps,animations");
        var categories = new HashSet<string>(
            categoriesValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase
        );
        var exporter = new AssetExporter(
            new ExportOptions
            {
                UoDirectory = Require(options, "uo"),
                WorkDirectory = Require(options, "work"),
                Scale = GetInt(options, "scale", 4),
                SheetSize = GetInt(options, "sheet-size", 1024),
                Padding = GetInt(options, "padding", 16),
                MaxAssets = GetInt(options, "max-assets", 0),
                Categories = categories
            }
        );
        exporter.Run();
    }

    private static int RunFinalize(Dictionary<string, string> options)
    {
        var finalizer = new AssetFinalizer(
            Require(options, "work"),
            Require(options, "upscaled"),
            Require(options, "output"),
            GetBool(options, "delete-upscaled", false)
        );
        PackReport report = finalizer.Run();
        return report.MissingAssets == 0 && report.Errors == 0 ? 0 : 2;
    }

    private static int RunValidate(Dictionary<string, string> options) =>
        PackValidator.Run(Require(options, "work"), Require(options, "output")) ? 0 : 2;

    private static Dictionary<string, string> ParseOptions(IEnumerable<string> values)
    {
        string[] args = values.ToArray();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Expected an option, got: {arg}");

            string key = arg[2..];
            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Missing value for --{key}");

            result[key] = args[++i];
        }

        return result;
    }

    private static string Require(Dictionary<string, string> options, string key) =>
        options.TryGetValue(key, out string value)
            ? value
            : throw new ArgumentException($"Missing required option --{key}");

    private static string Get(Dictionary<string, string> options, string key, string fallback) =>
        options.TryGetValue(key, out string value) ? value : fallback;

    private static int GetInt(Dictionary<string, string> options, string key, int fallback) =>
        options.TryGetValue(key, out string value)
            ? int.Parse(value, System.Globalization.CultureInfo.InvariantCulture)
            : fallback;

    private static bool GetBool(Dictionary<string, string> options, string key, bool fallback) =>
        options.TryGetValue(key, out string value) ? bool.Parse(value) : fallback;

    private static void PrintHelp()
    {
        Console.WriteLine(
            """
            TazUO HD Assets pipeline

            export   --uo <UO directory> --work <work directory>
                     [--scale 2|4] [--sheet-size 1024] [--padding 16]
                     [--categories land,art,gumps,texmaps,animations]
                     [--max-assets 0]

            finalize --work <work directory> --upscaled <Upscayl output directory>
                     --output <ExternalImages directory> [--delete-upscaled true|false]

            validate --work <work directory> --output <ExternalImages directory>

            pack     --work <work directory> --input <ExternalImages directory>
                     --output <tuoassets.hdpack path>

            plan     --work <work directory>
            """
        );
    }
}
