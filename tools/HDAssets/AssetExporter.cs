using ClassicUO.Assets;
using ClassicUO.Utility;
using SixLabors.ImageSharp.PixelFormats;

namespace ClassicUO.Tools.HDAssets;

internal sealed class ExportOptions
{
    public string UoDirectory { get; init; }
    public string WorkDirectory { get; init; }
    public int Scale { get; init; } = 4;
    public int SheetSize { get; init; } = 1024;
    public int Padding { get; init; } = 16;
    public int MaxAssets { get; init; }
    public HashSet<string> Categories { get; init; } = new HashSet<string>(
        new[] { "land", "art", "gumps", "texmaps", "animations" },
        StringComparer.OrdinalIgnoreCase
    );
}

internal sealed class AssetExporter
{
    private const int MaxRuntimeTextureSize = 4096;
    private readonly ExportOptions _options;
    private readonly PipelineManifest _manifest;
    private readonly string _sourceDirectory;
    private readonly SheetBuilder _sheets;
    private int _exported;

    public AssetExporter(ExportOptions options)
    {
        _options = options;
        _sourceDirectory = Path.Combine(options.WorkDirectory, "source");
        _manifest = new PipelineManifest
        {
            Scale = options.Scale,
            SheetSize = options.SheetSize,
            Padding = options.Padding,
            UoDirectory = Path.GetFullPath(options.UoDirectory),
            Categories = options.Categories.OrderBy(x => x).ToArray(),
            MaxAssets = options.MaxAssets
        };
        _sheets = new SheetBuilder(
            Path.Combine(options.WorkDirectory, "sheets"),
            options.SheetSize,
            options.Padding
        );
    }

    public PipelineManifest Run()
    {
        ValidateOptions();
        Directory.CreateDirectory(_options.WorkDirectory);
        Directory.CreateDirectory(_sourceDirectory);

        Console.WriteLine($"Loading Ultima Online files from: {_options.UoDirectory}");
        using var files = new UOFileManager(ClientVersion.CV_7010400, _options.UoDirectory);
        files.Load(useVerdata: false, lang: "enu", graphicsDevice: null);

        if (Wants("land"))
            ExportLand(files);
        if (Wants("art") && !ReachedLimit)
            ExportArt(files);
        if (Wants("gumps") && !ReachedLimit)
            ExportGumps(files);
        if (Wants("texmaps") && !ReachedLimit)
            ExportTexmaps(files);
        if (Wants("animations") && !ReachedLimit)
            ExportAnimations(files);

        _sheets.Finish();
        string manifestPath = Path.Combine(_options.WorkDirectory, "manifest.json");
        _manifest.Save(manifestPath);

        Console.WriteLine($"Exported {_exported:N0} assets into {_manifest.Assets.Select(x => x.Sheet).Distinct().Count():N0} sheets.");
        foreach ((string category, int count) in _manifest.ExportedByCategory.OrderBy(x => x.Key))
            Console.WriteLine($"  {category}: {count:N0}");
        foreach ((string reason, int count) in _manifest.SkippedByReason.OrderBy(x => x.Key))
            Console.WriteLine($"  skipped/{reason}: {count:N0}");
        Console.WriteLine($"Manifest: {manifestPath}");

        return _manifest;
    }

    private void ExportLand(UOFileManager files)
    {
        Console.WriteLine("Exporting land art...");
        int count = Math.Min(ArtLoader.MAX_LAND_DATA_INDEX_COUNT, files.Arts.File.Entries.Length);

        for (uint id = 0; id < count && !ReachedLimit; id++)
        {
            ArtInfo info = files.Arts.GetArt(id);
            AddAsset(
                "land",
                $"0x{id:X4}",
                $"land/0x{id:X4}.png",
                info.Pixels,
                info.Width,
                info.Height,
                restoreGrayscaleMask: false
            );
        }
    }

    private void ExportArt(UOFileManager files)
    {
        Console.WriteLine("Exporting static art...");

        for (
            uint index = ArtLoader.MAX_LAND_DATA_INDEX_COUNT;
            index < files.Arts.File.Entries.Length && !ReachedLimit;
            index++
        )
        {
            uint id = index - ArtLoader.MAX_LAND_DATA_INDEX_COUNT;
            ArtInfo info = files.Arts.GetArt(index);
            AddAsset(
                "art",
                $"0x{id:X4}",
                $"art/0x{id:X4}.png",
                info.Pixels,
                info.Width,
                info.Height,
                restoreGrayscaleMask: true
            );
        }
    }

    private void ExportGumps(UOFileManager files)
    {
        Console.WriteLine("Exporting gumps...");

        for (uint id = 0; id < files.Gumps.File.Entries.Length && !ReachedLimit; id++)
        {
            GumpInfo info = files.Gumps.GetGump(id);
            AddAsset(
                "gumps",
                $"0x{id:X4}",
                $"gumps/0x{id:X4}.png",
                info.Pixels,
                info.Width,
                info.Height,
                restoreGrayscaleMask: true
            );
        }
    }

    private void ExportTexmaps(UOFileManager files)
    {
        Console.WriteLine("Exporting terrain textures...");

        for (uint id = 0; id < files.Texmaps.File.Entries.Length && !ReachedLimit; id++)
        {
            TexmapInfo info = files.Texmaps.GetTexmap(id);
            AddAsset(
                "texmaps",
                $"0x{id:X4}",
                $"texmaps/0x{id:X4}.png",
                info.Pixels,
                info.Width,
                info.Height,
                restoreGrayscaleMask: false
            );
        }
    }

    private void ExportAnimations(UOFileManager files)
    {
        Console.WriteLine("Exporting animation frames...");

        for (int body = 0; body < 8192 && !ReachedLimit; body++)
        {
            try
            {
                ushort hue = 0;
                AnimationFlags flags = AnimationFlags.None;
                AnimationsLoader.AnimationDirection[] indices = files.Animations
                    .GetIndices(
                        files.Version,
                        (ushort)body,
                        ref hue,
                        ref flags,
                        out int fileIndex,
                        out AnimationGroupsType animationType
                    )
                    .ToArray();

                if (indices.Length == 0)
                    continue;

                bool useUop = (flags & AnimationFlags.UseUopAnimation) != 0;
                int actionCount = useUop
                    ? indices.Length
                    : indices.Length / AnimationsLoader.MAX_DIRECTIONS;

                for (int action = 0; action < actionCount && !ReachedLimit; action++)
                {
                    for (
                        int direction = 0;
                        direction < AnimationsLoader.MAX_DIRECTIONS && !ReachedLimit;
                        direction++
                    )
                    {
                        Span<AnimationsLoader.FrameInfo> frames;

                        if (useUop)
                        {
                            AnimationsLoader.AnimationDirection index = indices[action];
                            frames = files.Animations.ReadUOPAnimationFrames(
                                (ushort)body,
                                (byte)action,
                                (byte)direction,
                                animationType,
                                fileIndex,
                                index
                            );
                        }
                        else
                        {
                            AnimationsLoader.AnimationDirection index =
                                indices[action * AnimationsLoader.MAX_DIRECTIONS + direction];
                            frames = files.Animations.ReadMULAnimationFrames(fileIndex, index);
                        }

                        foreach (ref AnimationsLoader.FrameInfo frame in frames)
                        {
                            if (ReachedLimit)
                                break;

                            AddAsset(
                                "animations",
                                $"0x{body:X4}/{action}/{direction}/{frame.Num}",
                                $"animations/0x{body:X4}/{action}/{direction}/{frame.Num}.png",
                                frame.Pixels,
                                frame.Width,
                                frame.Height,
                                restoreGrayscaleMask: true
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CountSkipped("animation-read-error");
                Console.Error.WriteLine($"Skipping animation body 0x{body:X4}: {ex.Message}");
            }

            if (body > 0 && body % 256 == 0)
                Console.WriteLine($"  scanned bodies: {body:N0}; exported assets: {_exported:N0}");
        }
    }

    private void AddAsset(
        string category,
        string key,
        string relativePath,
        ReadOnlySpan<uint> pixels,
        int width,
        int height,
        bool restoreGrayscaleMask
    )
    {
        if (ReachedLimit)
            return;

        if (width <= 0 || height <= 0 || pixels.IsEmpty)
        {
            CountSkipped("empty");
            return;
        }

        if (
            width * _options.Scale > MaxRuntimeTextureSize
            || height * _options.Scale > MaxRuntimeTextureSize
        )
        {
            CountSkipped("over-4096-after-scale");
            return;
        }

        Rgba32[] sourcePixels = ImageTools.ConvertPixels(pixels, width, height);
        Rgba32[] aiPixels = ImageTools.CreateOpaqueAiInput(sourcePixels, width, height);
        if (aiPixels == null)
        {
            CountSkipped("transparent");
            return;
        }

        string normalizedRelativePath = relativePath.Replace('\\', '/');
        ImageTools.Save(
            Path.Combine(_sourceDirectory, normalizedRelativePath),
            sourcePixels,
            width,
            height
        );

        var entry = new AssetEntry
        {
            Category = category,
            Key = key,
            SourcePath = normalizedRelativePath,
            OutputPath = ImageTools.AddScaleSuffix(normalizedRelativePath, _options.Scale),
            Width = width,
            Height = height,
            RestoreGrayscaleMask = restoreGrayscaleMask
        };
        _sheets.Place(aiPixels, width, height, entry);
        _manifest.Assets.Add(entry);
        _manifest.ExportedByCategory[category] =
            _manifest.ExportedByCategory.GetValueOrDefault(category) + 1;
        _exported++;

        if (_exported % 1000 == 0)
            Console.WriteLine($"  exported assets: {_exported:N0}");
    }

    private bool Wants(string category) => _options.Categories.Contains(category);
    private bool ReachedLimit => _options.MaxAssets > 0 && _exported >= _options.MaxAssets;

    private void CountSkipped(string reason)
    {
        _manifest.SkippedByReason[reason] =
            _manifest.SkippedByReason.GetValueOrDefault(reason) + 1;
    }

    private void ValidateOptions()
    {
        if (!Directory.Exists(_options.UoDirectory))
            throw new DirectoryNotFoundException(_options.UoDirectory);
        if (_options.Scale is not (2 or 4))
            throw new ArgumentOutOfRangeException(nameof(_options.Scale), "Scale must be 2 or 4.");
        if (_options.SheetSize < 128)
            throw new ArgumentOutOfRangeException(nameof(_options.SheetSize));
        if (_options.Padding < 1)
            throw new ArgumentOutOfRangeException(nameof(_options.Padding));
    }
}
