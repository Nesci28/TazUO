using ClassicUO.Assets;
using ClassicUO.Utility.Logging;
using System;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Texmaps
{
    public sealed class Texmap
    {
        private readonly TextureAtlas _atlas;
        private readonly TextureAtlas _hdAtlas;
        private readonly SpriteInfo[] _spriteInfos;
        private readonly PixelPicker _picker = new PixelPicker(true);
        private readonly TexmapsLoader _texmapsLoader;

        public Texmap(TexmapsLoader texmapsLoader, GraphicsDevice device)
        {
            _texmapsLoader = texmapsLoader;
            _atlas = new TextureAtlas(device, 2048, 2048, SurfaceFormat.Color);
            _hdAtlas = new TextureAtlas(
                device,
                2048,
                2048,
                SurfaceFormat.Color,
                SamplerState.LinearClamp
            );
            _spriteInfos = new SpriteInfo[texmapsLoader.File.Entries.Length];
        }

        public ref readonly SpriteInfo GetTexmap(uint idx)
        {
            if (idx >= _spriteInfos.Length)
                return ref SpriteInfo.Empty;

            ref SpriteInfo spriteInfo = ref _spriteInfos[idx];

            if (spriteInfo.Texture == null)
            {
                TexmapInfo texmapInfo = ExternalImageLoader.Instance.LoadTexmapTexture(idx);
                bool loadedFromExternal = !texmapInfo.Pixels.IsEmpty;

                if (loadedFromExternal)
                {
                    TexmapInfo original = _texmapsLoader.GetTexmap(idx);
                    int sourceScale = Math.Max(1, texmapInfo.SourceScale);
                    int expectedWidth = original.Width * sourceScale;
                    int expectedHeight = original.Height * sourceScale;

                    if (
                        original.Pixels.IsEmpty
                        || texmapInfo.Width != expectedWidth
                        || texmapInfo.Height != expectedHeight
                        || texmapInfo.Width > 2048
                        || texmapInfo.Height > 2048
                    )
                    {
                        Log.Warn(
                            $"Ignoring external texmap 0x{idx:X}: got {texmapInfo.Width}x{texmapInfo.Height} " +
                            $"for @{sourceScale}x, expected {expectedWidth}x{expectedHeight}."
                        );
                        ExternalImageLoader.Instance.RejectTexmapOverride(idx);
                        texmapInfo = original;
                        loadedFromExternal = false;
                    }
                }

                if (texmapInfo.Pixels.IsEmpty)
                    texmapInfo = _texmapsLoader.GetTexmap(idx);

                if (!texmapInfo.Pixels.IsEmpty)
                {
                    int sourceScale = Math.Max(1, texmapInfo.SourceScale);
                    TextureAtlas atlas = sourceScale > 1 ? _hdAtlas : _atlas;
                    spriteInfo.Texture = atlas.AddSprite(
                        texmapInfo.Pixels,
                        texmapInfo.Width,
                        texmapInfo.Height,
                        out spriteInfo.UV,
                        padding: sourceScale > 1 ? 1 : 0
                    );
                    spriteInfo.SourceScale = sourceScale;

                    _picker.Set(idx, texmapInfo.Width, texmapInfo.Height, texmapInfo.Pixels, sourceScale);

                    if (loadedFromExternal)
                        ExternalImageLoader.Instance.ClearTexmapPixelCache(idx);
                }
            }

            return ref spriteInfo;
        }
    }
}
