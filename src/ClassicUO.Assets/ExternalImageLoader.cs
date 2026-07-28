using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Assets
{
    public class ExternalImageLoader
    {
        private const string IMAGES_FOLDER = "ExternalImages",
            GUMP_EXTERNAL_FOLDER = "gumps",
            ART_EXTERNAL_FOLDER = "art",
            LAND_EXTERNAL_FOLDER = "land",
            TEXMAP_EXTERNAL_FOLDER = "texmaps",
            ANIMATION_EXTERNAL_FOLDER = "animations";

        private readonly struct ExternalImageFile
        {
            public ExternalImageFile(string path, int sourceScale)
            {
                Path = path;
                SourceScale = sourceScale;
            }

            public string Path { get; }
            public int SourceScale { get; }
        }

        private string exePath;
        private string _uoDirectory;

        private Dictionary<string, Texture2D> EmbeddedArt = new Dictionary<string, Texture2D>();
        private Dictionary<string, Texture2D> _zipNamedTextures = new Dictionary<string, Texture2D>();
        private Texture2D _emptyTexture;

        private Dictionary<uint, ExternalImageFile> gump_availableFiles = new Dictionary<uint, ExternalImageFile>();
        private Dictionary<uint, (uint[] pixels, int width, int height, int sourceScale)> gump_textureCache = new Dictionary<uint, (uint[], int, int, int)>();

        private Dictionary<uint, ExternalImageFile> art_availableFiles = new Dictionary<uint, ExternalImageFile>();
        private Dictionary<uint, (uint[] pixels, int width, int height, int sourceScale)> art_textureCache = new Dictionary<uint, (uint[], int, int, int)>();

        private Dictionary<uint, ExternalImageFile> land_availableFiles = new Dictionary<uint, ExternalImageFile>();
        private Dictionary<uint, (uint[] pixels, int width, int height, int sourceScale)> land_textureCache = new Dictionary<uint, (uint[], int, int, int)>();

        private Dictionary<uint, ExternalImageFile> texmap_availableFiles = new Dictionary<uint, ExternalImageFile>();
        private Dictionary<uint, (uint[] pixels, int width, int height, int sourceScale)> texmap_textureCache = new Dictionary<uint, (uint[], int, int, int)>();

        private Dictionary<ulong, ExternalImageFile> animation_availableFiles = new Dictionary<ulong, ExternalImageFile>();
        private Dictionary<ulong, (uint[] pixels, int width, int height, int sourceScale)> animation_textureCache = new Dictionary<ulong, (uint[], int, int, int)>();
        private Dictionary<ulong, byte[]> animation_zipFiles = new Dictionary<ulong, byte[]>();
        private readonly object _animationCacheLock = new object();

        public bool HasHighResolutionGumps { get; private set; }
        public bool HasHighResolutionArt { get; private set; }
        public bool HasHighResolutionLand { get; private set; }
        public bool HasHighResolutionTexmaps { get; private set; }
        public bool HasHighResolutionAnimations { get; private set; }
        public bool HasHighResolutionWorldImages =>
            HasHighResolutionArt
            || HasHighResolutionLand
            || HasHighResolutionTexmaps
            || HasHighResolutionAnimations;
        public bool HasHighResolutionImages => HasHighResolutionGumps || HasHighResolutionWorldImages;

        public GraphicsDevice GraphicsDevice { set; get; }

        public static ExternalImageLoader _instance;
        public static ExternalImageLoader Instance => _instance ?? (_instance = new ExternalImageLoader());

        public bool TryGetEmbeddedTexture(string name, out Texture2D texture)
        {
            if (EmbeddedArt.TryGetValue(name, out texture))
            {
                return true;
            }

            if (_emptyTexture == null && GraphicsDevice != null)
            {
                _emptyTexture = new Texture2D(GraphicsDevice, 1, 1);
                _emptyTexture.SetData(new Color[] { Color.Transparent });
            }

            texture = _emptyTexture;
            return false;
        }

        public bool TryGetNamedZipTexture(string name, out Texture2D texture)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                texture = null;
                return false;
            }
            return _zipNamedTextures.TryGetValue(name, out texture);
        }

        public Texture2D GetImageTexture(string fullImagePath)
        {
            Texture2D texture = null;

            if (GraphicsDevice != null && File.Exists(fullImagePath))
            {
                FileStream titleStream = File.OpenRead(fullImagePath);
                texture = Texture2D.FromStream(GraphicsDevice, titleStream);
                titleStream.Close();
                var buffer = new Color[texture.Width * texture.Height];
                texture.GetData(buffer);

                for (int i = 0; i < buffer.Length; i++)
                    buffer[i] = Color.FromNonPremultiplied(buffer[i].R, buffer[i].G, buffer[i].B, buffer[i].A);

                texture.SetData(buffer);
            }

            return texture;
        }

        public GumpInfo LoadGumpTexture(uint graphic)
        {
            if (!gump_availableFiles.TryGetValue(graphic, out ExternalImageFile imageFile))
                return new GumpInfo();

            if (gump_textureCache.TryGetValue(graphic, out (uint[] pixels, int width, int height, int sourceScale) cached))
            {
                return new GumpInfo()
                {
                    Pixels = cached.pixels,
                    Width = cached.width,
                    Height = cached.height,
                    SourceScale = cached.sourceScale
                };
            }

            string fullImagePath = imageFile.Path;
            if (GraphicsDevice != null && File.Exists(fullImagePath))
            {
                try
                {
                    Texture2D tempTexture;
                    if (fullImagePath.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                    {
                        tempTexture = LoadBmp(GraphicsDevice, fullImagePath);
                    }
                    else
                    {
                        using FileStream titleStream = File.OpenRead(fullImagePath);
                        tempTexture = Texture2D.FromStream(GraphicsDevice, titleStream);
                    }

                    if (tempTexture == null)
                        return new GumpInfo();

                    FixPNGAlpha(ref tempTexture);

                    uint[] pixels = GetPixels(tempTexture);
                    int width = tempTexture.Width;
                    int height = tempTexture.Height;
                    gump_textureCache.Add(graphic, (pixels, width, height, imageFile.SourceScale));
                    tempTexture.Dispose();

                    return new GumpInfo()
                    {
                        Pixels = pixels,
                        Width = width,
                        Height = height,
                        SourceScale = imageFile.SourceScale
                    };
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to load gump image '{fullImagePath}': {ex.Message}");
                }
            }

            return new GumpInfo();
        }

        public ArtInfo LoadArtTexture(uint graphic)
        {
            if (!art_availableFiles.TryGetValue(graphic, out ExternalImageFile imageFile))
                return new ArtInfo();

            if (art_textureCache.TryGetValue(graphic, out (uint[] pixels, int width, int height, int sourceScale) cached))
            {
                return new ArtInfo()
                {
                    Pixels = cached.pixels,
                    Width = cached.width,
                    Height = cached.height,
                    SourceScale = cached.sourceScale
                };
            }

            string fullImagePath = imageFile.Path;
            if (GraphicsDevice != null && File.Exists(fullImagePath))
            {
                try
                {
                    Texture2D tempTexture;
                    if (fullImagePath.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                    {
                        tempTexture = LoadBmp(GraphicsDevice, fullImagePath);
                    }
                    else
                    {
                        using FileStream titleStream = File.OpenRead(fullImagePath);
                        tempTexture = Texture2D.FromStream(GraphicsDevice, titleStream);
                    }

                    if (tempTexture == null)
                        return new ArtInfo();

                    FixPNGAlpha(ref tempTexture);

                    uint[] pixels = GetPixels(tempTexture);
                    int width = tempTexture.Width;
                    int height = tempTexture.Height;
                    art_textureCache.Add(graphic, (pixels, width, height, imageFile.SourceScale));
                    tempTexture.Dispose();

                    return new ArtInfo()
                    {
                        Pixels = pixels,
                        Width = width,
                        Height = height,
                        SourceScale = imageFile.SourceScale
                    };
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to load art image '{fullImagePath}': {ex.Message}");
                }
            }

            return new ArtInfo();
        }

        public ArtInfo LoadLandTexture(uint graphic)
        {
            if (
                TryLoadExternalImage(
                    land_availableFiles,
                    land_textureCache,
                    graphic,
                    "land",
                    out uint[] pixels,
                    out int width,
                    out int height,
                    out int sourceScale
                )
            )
            {
                return new ArtInfo
                {
                    Pixels = pixels,
                    Width = width,
                    Height = height,
                    SourceScale = sourceScale
                };
            }

            return new ArtInfo();
        }

        public TexmapInfo LoadTexmapTexture(uint graphic)
        {
            if (
                TryLoadExternalImage(
                    texmap_availableFiles,
                    texmap_textureCache,
                    graphic,
                    "texmap",
                    out uint[] pixels,
                    out int width,
                    out int height,
                    out int sourceScale
                )
            )
            {
                return new TexmapInfo
                {
                    Pixels = pixels,
                    Width = width,
                    Height = height,
                    SourceScale = sourceScale
                };
            }

            return new TexmapInfo();
        }

        public ArtInfo LoadAnimationFrameTexture(
            ushort body,
            byte action,
            byte direction,
            int frame
        )
        {
            ulong key = PackAnimationFrameKey(body, action, direction, frame);
            ExternalImageFile imageFile;
            byte[] encodedBytes = null;

            lock (_animationCacheLock)
            {
                if (!animation_availableFiles.TryGetValue(key, out imageFile))
                    return new ArtInfo();

                animation_zipFiles.TryGetValue(key, out encodedBytes);

                if (
                    animation_textureCache.TryGetValue(
                        key,
                        out (uint[] pixels, int width, int height, int sourceScale) cached
                    )
                )
                {
                    return new ArtInfo
                    {
                        Pixels = cached.pixels,
                        Width = cached.width,
                        Height = cached.height,
                        SourceScale = cached.sourceScale
                    };
                }
            }

            string fullImagePath = imageFile.Path;
            if (
                GraphicsDevice == null
                || encodedBytes == null && !File.Exists(fullImagePath)
            )
                return new ArtInfo();

            try
            {
                Texture2D tempTexture;
                if (encodedBytes != null)
                {
                    using var imageStream = new MemoryStream(encodedBytes, false);
                    tempTexture = Texture2D.FromStream(GraphicsDevice, imageStream);
                }
                else if (fullImagePath.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                    tempTexture = LoadBmp(GraphicsDevice, fullImagePath);
                else
                {
                    using FileStream imageStream = File.OpenRead(fullImagePath);
                    tempTexture = Texture2D.FromStream(GraphicsDevice, imageStream);
                }

                if (tempTexture == null)
                    return new ArtInfo();

                FixPNGAlpha(ref tempTexture);
                uint[] pixels = GetPixels(tempTexture);
                int width = tempTexture.Width;
                int height = tempTexture.Height;
                tempTexture.Dispose();

                lock (_animationCacheLock)
                {
                    animation_textureCache[key] = (
                        pixels,
                        width,
                        height,
                        imageFile.SourceScale
                    );
                }

                return new ArtInfo
                {
                    Pixels = pixels,
                    Width = width,
                    Height = height,
                    SourceScale = imageFile.SourceScale
                };
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to load animation frame image '{fullImagePath}': {ex.Message}");
                return new ArtInfo();
            }
        }

        private bool TryLoadExternalImage(
            Dictionary<uint, ExternalImageFile> availableFiles,
            Dictionary<uint, (uint[] pixels, int width, int height, int sourceScale)> textureCache,
            uint graphic,
            string category,
            out uint[] pixels,
            out int width,
            out int height,
            out int sourceScale
        )
        {
            pixels = Array.Empty<uint>();
            width = 0;
            height = 0;
            sourceScale = 1;

            if (!availableFiles.TryGetValue(graphic, out ExternalImageFile imageFile))
                return false;

            if (textureCache.TryGetValue(graphic, out var cached))
            {
                pixels = cached.pixels;
                width = cached.width;
                height = cached.height;
                sourceScale = cached.sourceScale;
                return true;
            }

            string fullImagePath = imageFile.Path;
            if (GraphicsDevice == null || !File.Exists(fullImagePath))
                return false;

            try
            {
                Texture2D tempTexture;
                if (fullImagePath.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                    tempTexture = LoadBmp(GraphicsDevice, fullImagePath);
                else
                {
                    using FileStream imageStream = File.OpenRead(fullImagePath);
                    tempTexture = Texture2D.FromStream(GraphicsDevice, imageStream);
                }

                if (tempTexture == null)
                    return false;

                FixPNGAlpha(ref tempTexture);
                pixels = GetPixels(tempTexture);
                width = tempTexture.Width;
                height = tempTexture.Height;
                sourceScale = imageFile.SourceScale;
                textureCache[graphic] = (pixels, width, height, sourceScale);
                tempTexture.Dispose();
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to load {category} image '{fullImagePath}': {ex.Message}");
                return false;
            }
        }

        private static Texture2D LoadBmp(GraphicsDevice gd, string path)
        {
            byte[] file = File.ReadAllBytes(path);
            if (file.Length < 54 || file[0] != 'B' || file[1] != 'M')
                return null;

            int dataOffset = BitConverter.ToInt32(file, 10);
            int headerSize = BitConverter.ToInt32(file, 14);
            if (headerSize < 40)
                return null;

            int width = BitConverter.ToInt32(file, 18);
            int rawHeight = BitConverter.ToInt32(file, 22);
            bool topDown = rawHeight < 0;
            int height = Math.Abs(rawHeight);
            short bpp = BitConverter.ToInt16(file, 28);

            if (bpp != 24 && bpp != 32)
            {
                Log.Error($"Unsupported BMP bit depth {bpp} in '{path}'");
                return null;
            }

            int bytesPerPixel = bpp / 8;
            int rowStride = width * bytesPerPixel;
            int rowPadding = (4 - (rowStride % 4)) % 4;
            int rowBytes = rowStride + rowPadding;

            var texture = new Texture2D(gd, width, height);
            var pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                int srcRow = topDown ? y : (height - 1 - y);
                int srcOffset = dataOffset + srcRow * rowBytes;

                for (int x = 0; x < width; x++)
                {
                    int srcIdx = srcOffset + x * bytesPerPixel;
                    byte b = file[srcIdx];
                    byte g = file[srcIdx + 1];
                    byte r = file[srcIdx + 2];
                    byte a = (bytesPerPixel == 4) ? file[srcIdx + 3] : (byte)255;

                    pixels[y * width + x] = new Color(r, g, b, a);
                }
            }

            texture.SetData(pixels);
            return texture;
        }

        private uint[] GetPixels(Texture2D texture)
        {
            if (texture == null)
            {
                return new uint[0];
            }

            var pixelColors = new Color[texture.Width * texture.Height];
            texture.GetData<Color>(pixelColors);

            uint[] pixels = new uint[pixelColors.Length];
            for (int i = 0; i < pixelColors.Length; i++)
            {
                pixels[i] = pixelColors[i].PackedValue;
            }

            return pixels;
        }

        private static string[] FindImageFiles(string directory)
        {
            var results = new List<string>();
            results.AddRange(Directory.GetFiles(directory, "*.png", SearchOption.AllDirectories));
            results.AddRange(Directory.GetFiles(directory, "*.bmp", SearchOption.AllDirectories));
            return results.ToArray();
        }

        private static void RegisterAvailableFile(
            Dictionary<uint, ExternalImageFile> files,
            uint id,
            string path,
            int sourceScale
        )
        {
            // Prefer the highest-resolution explicitly tagged file when both legacy and HD
            // replacements exist for the same graphic.
            if (!files.TryGetValue(id, out ExternalImageFile current) || sourceScale > current.SourceScale)
                files[id] = new ExternalImageFile(path, sourceScale);
        }

        private static ulong PackAnimationFrameKey(
            uint body,
            uint action,
            uint direction,
            int frame
        ) => body | ((ulong)action << 16) | ((ulong)direction << 24) | ((ulong)(uint)frame << 32);

        private void RegisterAvailableAnimationFile(
            ulong key,
            string path,
            int sourceScale
        )
        {
            lock (_animationCacheLock)
            {
                if (
                    !animation_availableFiles.TryGetValue(key, out ExternalImageFile current)
                    || sourceScale > current.SourceScale
                )
                {
                    animation_availableFiles[key] = new ExternalImageFile(path, sourceScale);
                }
            }

            HasHighResolutionAnimations |= sourceScale > 1;
        }

        private static bool TryParseAnimationPathParts(
            string[] parts,
            int animationsIndex,
            out ulong key,
            out int sourceScale
        )
        {
            key = 0;
            sourceScale = 1;

            if (animationsIndex < 0 || parts.Length != animationsIndex + 5)
                return false;

            string frameName = Path.GetFileNameWithoutExtension(parts[animationsIndex + 4]);

            if (
                !TryParseId(parts[animationsIndex + 1], out uint body)
                || body > ushort.MaxValue
                || !TryParseId(parts[animationsIndex + 2], out uint action)
                || action >= AnimationsLoader.MAX_ACTIONS
                || !TryParseId(parts[animationsIndex + 3], out uint direction)
                || direction >= AnimationsLoader.MAX_DIRECTIONS
                || !TryParseAnimationFrameName(frameName, out uint frame, out sourceScale)
                || frame > ushort.MaxValue
            )
            {
                return false;
            }

            key = PackAnimationFrameKey(body, action, direction, (int)frame);
            return true;
        }

        private static bool TryParseAnimationFrameName(
            string value,
            out uint frame,
            out int sourceScale
        )
        {
            if (TryParseIdAndScale(value, out frame, out sourceScale))
                return true;

            if (value.EndsWith("@2x", StringComparison.OrdinalIgnoreCase))
                value = value.Substring(0, value.Length - 3);
            else if (value.EndsWith("@4x", StringComparison.OrdinalIgnoreCase))
                value = value.Substring(0, value.Length - 3);

            int separator = value.LastIndexOf('-');
            return separator >= 0 && TryParseId(value.Substring(separator + 1), out frame);
        }

        private static bool TryParseLooseAnimationPath(
            string animationsRoot,
            string fullPath,
            out ulong key,
            out int sourceScale
        )
        {
            string relativePath = Path.GetRelativePath(animationsRoot, fullPath);
            string[] relativeParts = relativePath.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries
            );
            string[] parts = new string[relativeParts.Length + 1];
            parts[0] = ANIMATION_EXTERNAL_FOLDER;
            Array.Copy(relativeParts, 0, parts, 1, relativeParts.Length);
            return TryParseAnimationPathParts(parts, 0, out key, out sourceScale);
        }

        private static bool TryParseZipAnimationPath(
            string entryPath,
            out ulong key,
            out int sourceScale
        )
        {
            string[] parts = entryPath.Replace('\\', '/').Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries
            );
            int animationsIndex = Array.FindIndex(
                parts,
                part => part.Equals(ANIMATION_EXTERNAL_FOLDER, StringComparison.OrdinalIgnoreCase)
            );
            return TryParseAnimationPathParts(parts, animationsIndex, out key, out sourceScale);
        }

        public void Load(string uoDirectory = null)
        {
            exePath = AppContext.BaseDirectory;
            _uoDirectory = uoDirectory;

            string gumpPath = Path.Combine(exePath, IMAGES_FOLDER, GUMP_EXTERNAL_FOLDER);

            if (Directory.Exists(gumpPath))
            {
                string[] files = FindImageFiles(gumpPath);

                for (int i = 0; i < files.Length; i++)
                {
                    string fname = Path.GetFileName(files[i]);
                    string baseName = Path.GetFileNameWithoutExtension(fname);
                    if (TryParseIdAndScale(baseName, out uint id, out int sourceScale))
                    {
                        RegisterAvailableFile(gump_availableFiles, id, files[i], sourceScale);
                        HasHighResolutionGumps |= sourceScale > 1;
                    }
                }
            }
            else
            {
                Directory.CreateDirectory(gumpPath);
            }

            string artPath = Path.Combine(exePath, IMAGES_FOLDER, ART_EXTERNAL_FOLDER);

            if (Directory.Exists(artPath))
            {
                string[] files = FindImageFiles(artPath);

                for (int i = 0; i < files.Length; i++)
                {
                    string fname = Path.GetFileName(files[i]);
                    string baseName = Path.GetFileNameWithoutExtension(fname);

                    if (TryParseIdAndScale(baseName, out uint gfx, out int sourceScale))
                    {
                        RegisterAvailableFile(art_availableFiles, gfx + 0x4000, files[i], sourceScale);
                        HasHighResolutionArt |= sourceScale > 1;
                    }
                }
            }
            else
            {
                Directory.CreateDirectory(artPath);
            }

            string landPath = Path.Combine(exePath, IMAGES_FOLDER, LAND_EXTERNAL_FOLDER);

            if (Directory.Exists(landPath))
            {
                string[] files = FindImageFiles(landPath);

                for (int i = 0; i < files.Length; i++)
                {
                    string baseName = Path.GetFileNameWithoutExtension(files[i]);
                    if (TryParseIdAndScale(baseName, out uint id, out int sourceScale))
                    {
                        RegisterAvailableFile(land_availableFiles, id, files[i], sourceScale);
                        HasHighResolutionLand |= sourceScale > 1;
                    }
                }
            }
            else
            {
                Directory.CreateDirectory(landPath);
            }

            string texmapPath = Path.Combine(exePath, IMAGES_FOLDER, TEXMAP_EXTERNAL_FOLDER);

            if (Directory.Exists(texmapPath))
            {
                string[] files = FindImageFiles(texmapPath);

                for (int i = 0; i < files.Length; i++)
                {
                    string baseName = Path.GetFileNameWithoutExtension(files[i]);
                    if (TryParseIdAndScale(baseName, out uint id, out int sourceScale))
                    {
                        RegisterAvailableFile(texmap_availableFiles, id, files[i], sourceScale);
                        HasHighResolutionTexmaps |= sourceScale > 1;
                    }
                }
            }
            else
            {
                Directory.CreateDirectory(texmapPath);
            }

            string animationPath = Path.Combine(
                exePath,
                IMAGES_FOLDER,
                ANIMATION_EXTERNAL_FOLDER
            );

            if (Directory.Exists(animationPath))
            {
                string[] files = FindImageFiles(animationPath);

                for (int i = 0; i < files.Length; i++)
                {
                    if (
                        TryParseLooseAnimationPath(
                            animationPath,
                            files[i],
                            out ulong key,
                            out int sourceScale
                        )
                    )
                    {
                        RegisterAvailableAnimationFile(key, files[i], sourceScale);
                    }
                    else
                    {
                        Log.Warn(
                            $"Ignoring external animation image '{files[i]}': expected " +
                            "animations/<body>/<action>/<direction>/<frame>[@2x|@4x].png."
                        );
                    }
                }
            }
            else
            {
                Directory.CreateDirectory(animationPath);
            }
        }

        public void LoadResourceAssets(GumpsLoader gumps)
        {
            Log.Debug("Loading resource assets");

            System.Reflection.Assembly assembly = GetType().Assembly;

            //Load all embedded art in gumpartassets folder
            string[] resourceNames = assembly.GetManifestResourceNames();

            foreach (string resourceName in resourceNames)
            {
                string path = assembly.GetName().Name + ".gumpartassets.";

                if (resourceName.StartsWith(path, StringComparison.Ordinal) && resourceName.EndsWith(".png", StringComparison.Ordinal))
                {
                    string fName = resourceName.Substring(path.Length);
                    Log.Debug("Loading PNG: " + fName);

                    try
                    {
                        Stream stream = assembly.GetManifestResourceStream(resourceName);

                        if (stream != null)
                        {
                            var texture = Texture2D.FromStream(GraphicsDevice, stream);

                            if (texture == null)
                            {
                                stream.Dispose();
                                continue;
                            }

                            FixPNGAlpha(ref texture);
                            EmbeddedArt.Add(fName, texture);
                            stream.Dispose();
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }
            }

            LoadTuoAssetsZips();
        }

        private static void FixPNGAlpha(ref Texture2D texture)
        {
            var buffer = new Color[texture.Width * texture.Height];
            texture.GetData(buffer);

            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = Color.FromNonPremultiplied(buffer[i].R, buffer[i].G, buffer[i].B, buffer[i].A);

            texture.SetData(buffer);
        }

        public void RegisterZipPNGs(ZipArchive archive)
        {
            if (GraphicsDevice == null) return;

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (!entry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                && !entry.Name.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)) continue;

                byte[] bytes;
                using (var ms = new MemoryStream())
                using (var es = entry.Open())
                {
                    es.CopyTo(ms);
                    bytes = ms.ToArray();
                }

                string entryPath = entry.FullName.Replace('\\', '/');
                if (
                    TryParseZipAnimationPath(
                        entryPath,
                        out ulong animationKey,
                        out int animationScale
                    )
                )
                {
                    RegisterAnimationFromBytes(animationKey, bytes, animationScale);
                    continue;
                }

                // Register as a named texture (full path and filename shortcut)
                RegisterNamedZipTexture(entryPath, bytes);
                if (!_zipNamedTextures.ContainsKey(entry.Name))
                    RegisterNamedZipTexture(entry.Name, bytes);

                // Also handle gumps/ and art/ ID-based overrides
                string[] parts = entryPath.Split('/');
                if (parts.Length >= 2)
                {
                    string folder = parts[parts.Length - 2];
                    string baseName = Path.GetFileNameWithoutExtension(entry.Name);

                    if (folder.Equals(GUMP_EXTERNAL_FOLDER, StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryParseIdAndScale(baseName, out uint id, out int sourceScale))
                            RegisterGumpFromBytes(id, bytes, sourceScale);
                    }
                    else if (folder.Equals(ART_EXTERNAL_FOLDER, StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryParseIdAndScale(baseName, out uint fileId, out int sourceScale))
                        {
                            uint graphicId = fileId + 0x4000;
                            RegisterArtFromBytes(graphicId, bytes, sourceScale);
                        }
                    }
                    else if (folder.Equals(LAND_EXTERNAL_FOLDER, StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryParseIdAndScale(baseName, out uint id, out int sourceScale))
                            RegisterLandFromBytes(id, bytes, sourceScale);
                    }
                    else if (folder.Equals(TEXMAP_EXTERNAL_FOLDER, StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryParseIdAndScale(baseName, out uint id, out int sourceScale))
                            RegisterTexmapFromBytes(id, bytes, sourceScale);
                    }
                }
            }
        }

        private static bool TryParseId(string value, out uint result)
        {
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return uint.TryParse(value.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out result);
            return uint.TryParse(value, out result);
        }

        private static bool TryParseIdAndScale(string value, out uint result, out int sourceScale)
        {
            sourceScale = 1;

            if (value.EndsWith("@2x", StringComparison.OrdinalIgnoreCase))
            {
                sourceScale = 2;
                value = value.Substring(0, value.Length - 3);
            }
            else if (value.EndsWith("@4x", StringComparison.OrdinalIgnoreCase))
            {
                sourceScale = 4;
                value = value.Substring(0, value.Length - 3);
            }

            return TryParseId(value, out result);
        }

        private static bool ShouldSkipEntry(string fullName)
        {
            string normalized = fullName.Replace('\\', '/');
            foreach (string seg in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (seg[0] == '_' || seg[0] == '.') return true;
            }
            return false;
        }

        private void LoadTuoAssetsZips()
        {
            const string ZIP_NAME = "tuoassets.zip";

            string exeZip = Path.Combine(exePath, ZIP_NAME);
            LoadTuoAssetsZip(exeZip);

            if (!string.IsNullOrEmpty(_uoDirectory))
            {
                string uoZip = Path.Combine(_uoDirectory, ZIP_NAME);
                if (!string.Equals(uoZip, exeZip, StringComparison.OrdinalIgnoreCase))
                    LoadTuoAssetsZip(uoZip);
            }
        }

        private void LoadTuoAssetsZip(string zipPath)
        {
            if (GraphicsDevice == null || !File.Exists(zipPath)) return;

            Log.Info($"Loading tuoassets.zip: {zipPath}");
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    if (!entry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                    && !entry.Name.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)) continue;
                    if (ShouldSkipEntry(entry.FullName)) continue;

                    byte[] bytes;
                    using (var ms = new MemoryStream())
                    using (var es = entry.Open())
                    {
                        es.CopyTo(ms);
                        bytes = ms.ToArray();
                    }

                    if (EmbeddedArt.ContainsKey(entry.Name))
                    {
                        try
                        {
                            using var ms = new MemoryStream(bytes);
                            var tex = Texture2D.FromStream(GraphicsDevice, ms);
                            if (tex == null) continue;
                            FixPNGAlpha(ref tex);
                            if (EmbeddedArt.TryGetValue(entry.Name, out Texture2D old)
                            && old != null && !old.IsDisposed)
                                old.Dispose();
                            EmbeddedArt[entry.Name] = tex;
                            Log.Debug($"tuoassets.zip overrode embedded asset: {entry.Name}");
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"tuoassets.zip: error overriding embedded asset '{entry.Name}': {ex.Message}");
                        }
                        continue;
                    }

                    string entryPath = entry.FullName.Replace('\\', '/');

                    if (
                        TryParseZipAnimationPath(
                            entryPath,
                            out ulong animationKey,
                            out int animationScale
                        )
                    )
                    {
                        RegisterAnimationFromBytes(animationKey, bytes, animationScale);
                        continue;
                    }

                    string[] parts = entryPath.Split('/');
                    if (parts.Length >= 2)
                    {
                        string folder = parts[parts.Length - 2];
                        string baseName = Path.GetFileNameWithoutExtension(entry.Name);

                        if (folder.Equals(GUMP_EXTERNAL_FOLDER, StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryParseIdAndScale(baseName, out uint id, out int sourceScale))
                                RegisterGumpFromBytes(id, bytes, sourceScale);
                        }
                        else if (folder.Equals(ART_EXTERNAL_FOLDER, StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryParseIdAndScale(baseName, out uint fileId, out int sourceScale))
                                RegisterArtFromBytes(fileId + 0x4000, bytes, sourceScale);
                        }
                        else if (folder.Equals(LAND_EXTERNAL_FOLDER, StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryParseIdAndScale(baseName, out uint id, out int sourceScale))
                                RegisterLandFromBytes(id, bytes, sourceScale);
                        }
                        else if (folder.Equals(TEXMAP_EXTERNAL_FOLDER, StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryParseIdAndScale(baseName, out uint id, out int sourceScale))
                                RegisterTexmapFromBytes(id, bytes, sourceScale);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"tuoassets.zip: error loading '{zipPath}': {ex.Message}");
            }
        }

        private void RegisterNamedZipTexture(string name, byte[] bytes)
        {
            if (GraphicsDevice == null) return;
            try
            {
                using var ms = new MemoryStream(bytes);
                var tex = Texture2D.FromStream(GraphicsDevice, ms);
                if (tex == null) return;
                FixPNGAlpha(ref tex);
                if (_zipNamedTextures.TryGetValue(name, out Texture2D existing) && existing != null && !existing.IsDisposed)
                    existing.Dispose();
                _zipNamedTextures[name] = tex;
            }
            catch (Exception ex) { Log.Error($"Error registering named zip texture '{name}': {ex.Message}"); }
        }

        private void RegisterGumpFromBytes(uint id, byte[] bytes, int sourceScale = 1)
        {
            if (GraphicsDevice == null) return;
            if (gump_availableFiles.TryGetValue(id, out ExternalImageFile current) && sourceScale < current.SourceScale) return;
            try
            {
                using var ms = new MemoryStream(bytes);
                var tex = Texture2D.FromStream(GraphicsDevice, ms);
                if (tex == null) return;
                FixPNGAlpha(ref tex);
                uint[] pixels = GetPixels(tex);
                int width = tex.Width, height = tex.Height;
                gump_textureCache[id] = (pixels, width, height, sourceScale);
                tex.Dispose();

                RegisterAvailableFile(gump_availableFiles, id, $"0x{id:X}", sourceScale);
                HasHighResolutionGumps |= sourceScale > 1;
            }
            catch (Exception ex) { Log.Error($"Error registering zip gump image {id}: {ex.Message}"); }
        }

        private void RegisterArtFromBytes(uint id, byte[] bytes, int sourceScale = 1)
        {
            if (GraphicsDevice == null) return;
            if (art_availableFiles.TryGetValue(id, out ExternalImageFile current) && sourceScale < current.SourceScale) return;
            try
            {
                using var ms = new MemoryStream(bytes);
                var tex = Texture2D.FromStream(GraphicsDevice, ms);
                if (tex == null) return;
                FixPNGAlpha(ref tex);
                uint[] pixels = GetPixels(tex);
                int width = tex.Width, height = tex.Height;
                art_textureCache[id] = (pixels, width, height, sourceScale);
                tex.Dispose();

                RegisterAvailableFile(art_availableFiles, id, $"0x{id:X}", sourceScale);
                HasHighResolutionArt |= sourceScale > 1;
            }
            catch (Exception ex) { Log.Error($"Error registering zip art PNG {id}: {ex.Message}"); }
        }

        private void RegisterLandFromBytes(uint id, byte[] bytes, int sourceScale = 1)
        {
            if (GraphicsDevice == null) return;
            if (land_availableFiles.TryGetValue(id, out ExternalImageFile current) && sourceScale < current.SourceScale) return;
            try
            {
                using var ms = new MemoryStream(bytes);
                var tex = Texture2D.FromStream(GraphicsDevice, ms);
                if (tex == null) return;
                FixPNGAlpha(ref tex);
                uint[] pixels = GetPixels(tex);
                int width = tex.Width, height = tex.Height;
                land_textureCache[id] = (pixels, width, height, sourceScale);
                tex.Dispose();

                RegisterAvailableFile(land_availableFiles, id, $"0x{id:X}", sourceScale);
                HasHighResolutionLand |= sourceScale > 1;
            }
            catch (Exception ex) { Log.Error($"Error registering zip land PNG {id}: {ex.Message}"); }
        }

        private void RegisterTexmapFromBytes(uint id, byte[] bytes, int sourceScale = 1)
        {
            if (GraphicsDevice == null) return;
            if (texmap_availableFiles.TryGetValue(id, out ExternalImageFile current) && sourceScale < current.SourceScale) return;
            try
            {
                using var ms = new MemoryStream(bytes);
                var tex = Texture2D.FromStream(GraphicsDevice, ms);
                if (tex == null) return;
                FixPNGAlpha(ref tex);
                uint[] pixels = GetPixels(tex);
                int width = tex.Width, height = tex.Height;
                texmap_textureCache[id] = (pixels, width, height, sourceScale);
                tex.Dispose();

                RegisterAvailableFile(texmap_availableFiles, id, $"0x{id:X}", sourceScale);
                HasHighResolutionTexmaps |= sourceScale > 1;
            }
            catch (Exception ex) { Log.Error($"Error registering zip texmap PNG {id}: {ex.Message}"); }
        }

        private void RegisterAnimationFromBytes(
            ulong key,
            byte[] bytes,
            int sourceScale = 1
        )
        {
            lock (_animationCacheLock)
            {
                if (
                    animation_availableFiles.TryGetValue(key, out ExternalImageFile current)
                    && sourceScale < current.SourceScale
                )
                {
                    return;
                }

                animation_zipFiles[key] = bytes;
            }

            RegisterAvailableAnimationFile(key, $"animation:{key}", sourceScale);
        }

        public void ClearArtPixelCache(uint graphic) => art_textureCache.Remove(graphic);

        public void ClearGumpPixelCache(uint graphic) => gump_textureCache.Remove(graphic);

        public void ClearLandPixelCache(uint graphic) => land_textureCache.Remove(graphic);

        public void ClearTexmapPixelCache(uint graphic) => texmap_textureCache.Remove(graphic);

        public void ClearAnimationFramePixelCache(
            ushort body,
            byte action,
            byte direction,
            int frame
        )
        {
            ulong key = PackAnimationFrameKey(body, action, direction, frame);
            lock (_animationCacheLock)
                animation_textureCache.Remove(key);
        }

        public void RejectArtOverride(uint graphic)
        {
            art_textureCache.Remove(graphic);
            art_availableFiles.Remove(graphic);
        }

        public void RejectGumpOverride(uint graphic)
        {
            gump_textureCache.Remove(graphic);
            gump_availableFiles.Remove(graphic);
        }

        public void RejectLandOverride(uint graphic)
        {
            land_textureCache.Remove(graphic);
            land_availableFiles.Remove(graphic);
        }

        public void RejectTexmapOverride(uint graphic)
        {
            texmap_textureCache.Remove(graphic);
            texmap_availableFiles.Remove(graphic);
        }

        public void RejectAnimationFrameOverride(
            ushort body,
            byte action,
            byte direction,
            int frame
        )
        {
            ulong key = PackAnimationFrameKey(body, action, direction, frame);
            lock (_animationCacheLock)
            {
                animation_textureCache.Remove(key);
                animation_availableFiles.Remove(key);
                animation_zipFiles.Remove(key);
            }
        }

        public void ClearAllPixelCaches()
        {
            art_textureCache.Clear();
            gump_textureCache.Clear();
            land_textureCache.Clear();
            texmap_textureCache.Clear();
            lock (_animationCacheLock)
                animation_textureCache.Clear();
        }
    }
}
