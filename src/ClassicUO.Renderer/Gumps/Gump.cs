using ClassicUO.Assets;
using ClassicUO.Utility.Logging;
using System;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Gumps
{
    public sealed class Gump
    {
        private const uint SMALL_MINIMAP_GUMP = 5010;
        private const uint LARGE_MINIMAP_GUMP = 5011;

        private readonly TextureAtlas _atlas;
        private readonly SpriteInfo[] _spriteInfos;
        private readonly PixelPicker _picker = new PixelPicker(true);
        private readonly GumpsLoader _gumpsLoader;

        public GumpsLoader GetGumpsLoader => _gumpsLoader;

        public Gump(GumpsLoader gumpsLoader, GraphicsDevice device)
        {
            _gumpsLoader = gumpsLoader;
            _atlas = new TextureAtlas(device, 4096, 4096, SurfaceFormat.Color);
            _spriteInfos = new SpriteInfo[gumpsLoader.File.Entries.Length];
        }

        public ref readonly SpriteInfo GetGump(uint idx)
        {
            if (idx >= _spriteInfos.Length)
                return ref SpriteInfo.Empty;

            ref SpriteInfo spriteInfo = ref _spriteInfos[idx];

            if (spriteInfo.Texture == null)
            {
                GumpInfo gumpInfo = ExternalImageLoader.Instance.LoadGumpTexture(idx);
                bool loadedFromPNG = !gumpInfo.Pixels.IsEmpty;

                if (loadedFromPNG && gumpInfo.SourceScale > 1)
                {
                    GumpInfo original = _gumpsLoader.GetGump(idx);
                    int expectedWidth = original.Width * gumpInfo.SourceScale;
                    int expectedHeight = original.Height * gumpInfo.SourceScale;
                    bool isRuntimeRasterizedMinimap =
                        idx == SMALL_MINIMAP_GUMP || idx == LARGE_MINIMAP_GUMP;

                    if (
                        isRuntimeRasterizedMinimap
                        || original.Pixels.IsEmpty
                        || gumpInfo.Width != expectedWidth
                        || gumpInfo.Height != expectedHeight
                        || gumpInfo.Width > 4096
                        || gumpInfo.Height > 4096
                    )
                    {
                        if (isRuntimeRasterizedMinimap)
                        {
                            Log.Warn(
                                $"Ignoring HD gump 0x{idx:X}: the minimap writes runtime map pixels " +
                                "into its 1x background and cannot safely use a scaled source yet."
                            );
                        }
                        else
                        {
                            Log.Warn(
                                $"Ignoring HD gump 0x{idx:X}: got {gumpInfo.Width}x{gumpInfo.Height} " +
                                $"for @{gumpInfo.SourceScale}x, expected {expectedWidth}x{expectedHeight}."
                            );
                        }
                        ExternalImageLoader.Instance.RejectGumpOverride(idx);
                        gumpInfo = original;
                        loadedFromPNG = false;
                    }
                    else
                    {
                        ExternalImageMaskRestorer.RestoreFromOriginal(
                            original.Pixels,
                            original.Width,
                            original.Height,
                            gumpInfo.Pixels,
                            gumpInfo.Width,
                            gumpInfo.Height,
                            gumpInfo.SourceScale
                        );
                    }
                }

                if (gumpInfo.Pixels.IsEmpty)
                {
                    gumpInfo = _gumpsLoader.GetGump(idx);
                }
                if (!gumpInfo.Pixels.IsEmpty)
                {
                    int sourceScale = Math.Max(1, gumpInfo.SourceScale);
                    spriteInfo.Texture = _atlas.AddSprite(
                        gumpInfo.Pixels,
                        gumpInfo.Width,
                        gumpInfo.Height,
                        out spriteInfo.UV
                    );
                    spriteInfo.SourceScale = sourceScale;

                    _picker.Set(idx, gumpInfo.Width, gumpInfo.Height, gumpInfo.Pixels, sourceScale);

                    // Clear the pixel cache from PNG Loader since it's now in the atlas
                    if (loadedFromPNG)
                    {
                        ExternalImageLoader.Instance.ClearGumpPixelCache(idx);
                    }
                }
            }

            return ref spriteInfo;
        }

        public bool PixelCheck(uint idx, int x, int y, double scale = 1f) => _picker.Get(idx, x, y, scale: scale);
    }
}
