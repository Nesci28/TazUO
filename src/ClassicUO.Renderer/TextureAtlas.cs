using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StbRectPackSharp;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ClassicUO.Renderer
{
    public class TextureAtlas : IDisposable
    {
        private readonly int _width,
            _height;
        private readonly SurfaceFormat _format;
        private readonly GraphicsDevice _device;
        private readonly List<Texture2D> _textureList;
        private readonly SamplerState _preferredSampler;
        private Packer _packer;
        private static readonly ConditionalWeakTable<Texture2D, SamplerState> _preferredSamplers =
            new ConditionalWeakTable<Texture2D, SamplerState>();

        public TextureAtlas(
            GraphicsDevice device,
            int width,
            int height,
            SurfaceFormat format,
            SamplerState preferredSampler = null
        )
        {
            _device = device;
            _width = width;
            _height = height;
            _format = format;
            _preferredSampler = preferredSampler;

            _textureList = new List<Texture2D>();
        }

        public int TexturesCount => _textureList.Count;

        public static bool TryGetPreferredSampler(
            Texture2D texture,
            out SamplerState preferredSampler
        ) => _preferredSamplers.TryGetValue(texture, out preferredSampler);

        public void EnsureCapacity(int estimatedWidth, int estimatedHeight)
        {
            // Ensure at least one texture exists before starting uploads
            // This prevents the very first texture creation from happening during
            // the critical upload loop, reducing initial spike
            if (_textureList.Count == 0)
            {
                CreateNewTexture2D();
            }

            // Note: We can't reliably pre-check available space without the packer's
            // internal state, so we just ensure a texture exists for the first upload
        }

        public unsafe Texture2D AddSprite(
            ReadOnlySpan<uint> pixels,
            int width,
            int height,
            out Rectangle pr,
            int padding = 0
        )
        {
            if (padding < 0)
                throw new ArgumentOutOfRangeException(nameof(padding));

            if (
                padding > 0
                && (width > _width - padding * 2 || height > _height - padding * 2)
            )
            {
                padding = 0;
            }

            int packedWidth = width + padding * 2;
            int packedHeight = height + padding * 2;
            int index = _textureList.Count - 1;

            if (index < 0)
            {
                index = 0;
                CreateNewTexture2D();
            }

            Rectangle packedRectangle;
            while (!_packer.PackRect(packedWidth, packedHeight, out packedRectangle))
            {
                CreateNewTexture2D();
                index = _textureList.Count - 1;
            }

            Texture2D texture = _textureList[index];
            pr = new Rectangle(
                packedRectangle.X + padding,
                packedRectangle.Y + padding,
                width,
                height
            );

            if (padding == 0)
            {
                fixed (uint* src = pixels)
                {
                    texture.SetDataPointerEXT(
                        0,
                        packedRectangle,
                        (IntPtr)src,
                        sizeof(uint) * width * height
                    );
                }
            }
            else
            {
                int pixelCount = packedWidth * packedHeight;
                uint[] rentedPixels = ArrayPool<uint>.Shared.Rent(pixelCount);

                try
                {
                    Span<uint> paddedPixels = rentedPixels.AsSpan(0, pixelCount);

                    for (int y = 0; y < height; y++)
                    {
                        ReadOnlySpan<uint> sourceRow = pixels.Slice(y * width, width);
                        int destinationOffset = (y + padding) * packedWidth;

                        paddedPixels.Slice(destinationOffset, padding).Fill(sourceRow[0]);
                        sourceRow.CopyTo(
                            paddedPixels.Slice(destinationOffset + padding, width)
                        );
                        paddedPixels
                            .Slice(destinationOffset + padding + width, padding)
                            .Fill(sourceRow[width - 1]);
                    }

                    ReadOnlySpan<uint> firstRow = paddedPixels.Slice(
                        padding * packedWidth,
                        packedWidth
                    );
                    for (int y = 0; y < padding; y++)
                    {
                        firstRow.CopyTo(paddedPixels.Slice(y * packedWidth, packedWidth));
                    }

                    ReadOnlySpan<uint> lastRow = paddedPixels.Slice(
                        (padding + height - 1) * packedWidth,
                        packedWidth
                    );
                    for (int y = padding + height; y < packedHeight; y++)
                    {
                        lastRow.CopyTo(paddedPixels.Slice(y * packedWidth, packedWidth));
                    }

                    fixed (uint* src = rentedPixels)
                    {
                        texture.SetDataPointerEXT(
                            0,
                            packedRectangle,
                            (IntPtr)src,
                            sizeof(uint) * pixelCount
                        );
                    }
                }
                finally
                {
                    ArrayPool<uint>.Shared.Return(rentedPixels);
                }
            }

            return texture;
        }

        private void CreateNewTexture2D()
        {
            Utility.Logging.Log.Trace($"creating texture: {_width}x{_height} {_format}");
            var texture = new Texture2D(_device, _width, _height, false, _format);
            _textureList.Add(texture);

            if (_preferredSampler != null)
                _preferredSamplers.Add(texture, _preferredSampler);

            _packer?.Dispose();
            _packer = new Packer(_width, _height);
        }

        public void SaveImages(string name)
        {
            for (int i = 0, count = TexturesCount; i < count; ++i)
            {
                Texture2D texture = _textureList[i];

                using (System.IO.FileStream stream = System.IO.File.Create($"atlas/{name}_atlas_{i}.png"))
                {
                    texture.SaveAsPng(stream, texture.Width, texture.Height);
                }
            }
        }

        public void Dispose()
        {
            foreach (Texture2D texture in _textureList)
            {
                _preferredSamplers.Remove(texture);

                if (!texture.IsDisposed)
                {
                    texture.Dispose();
                }
            }

            _packer.Dispose();
            _textureList.Clear();
        }
    }
}
